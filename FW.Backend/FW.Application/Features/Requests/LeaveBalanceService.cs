using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.Application.Features.Requests;

/// <summary>Số ngày phép năm mặc định (system_settings.annual_leave_days, thiếu thì 12).</summary>
public static class AnnualLeave
{
    public const decimal DefaultDays = 12;

    public static async Task<decimal> DefaultDaysAsync(ISystemSettingRepository settings, CancellationToken cancellationToken)
    {
        var raw = await settings.GetValueAsync(SystemSettingKeys.AnnualLeaveDays, cancellationToken);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : DefaultDays;
    }
}

public class LeaveBalanceFilter : PagedQuery
{
    /// <summary>Mặc định năm nay.</summary>
    public int? Year { get; set; }

    /// <summary>Admin/HR lọc chi nhánh; Quản lý luôn là chi nhánh mình.</summary>
    public int? BranchId { get; set; }
}

/// <summary>HR cộng / trừ quỹ phép năm (UC 5.5).</summary>
public class AdjustLeaveBalanceRequest
{
    [Required(ErrorMessage = "Vui lòng chọn nhân viên.")]
    public int? UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn năm.")]
    public int? Year { get; set; }

    /// <summary>Số ngày cộng (dương) / trừ (âm), bước 0,5 ngày.</summary>
    [Required(ErrorMessage = "Vui lòng nhập số ngày điều chỉnh.")]
    public decimal? DeltaDays { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do điều chỉnh.")]
    [MaxLength(RequestRules.MaxNoteLength, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string? Reason { get; set; }
}

/// <summary>Quỹ phép năm của một nhân viên. <see cref="IsDefault"/> = chưa khởi tạo, đang hiển thị số ngày mặc định.</summary>
public record LeaveBalanceRowDto(
    int UserId, string EmployeeCode, string FullName, string? AvatarUrl, int? BranchId, string? BranchName,
    int Year, decimal TotalDays, decimal UsedDays, decimal RemainingDays, decimal PendingDays, decimal AvailableDays, bool IsDefault);

public record LeaveBalanceAdjustmentDto(int Id, int Year, decimal DeltaDays, string Reason, int? CreatedBy, string? CreatedByName, DateTime CreatedAt)
{
    public static LeaveBalanceAdjustmentDto From(LeaveBalanceAdjustmentDetail a) =>
        new(a.Id, a.Year, a.DeltaDays, a.Reason, a.CreatedBy, a.CreatedByName, a.CreatedAt);
}

public interface ILeaveBalanceService
{
    /// <summary>Quỹ phép của nhân viên (chưa nghỉ việc): Admin/HR mọi chi nhánh, Quản lý chi nhánh mình.</summary>
    Task<PaginatedResult<LeaveBalanceRowDto>> GetBalancesAsync(LeaveBalanceFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Admin/HR cộng / trừ tổng quỹ phép năm kèm lý do (lưu lịch sử).</summary>
    Task<LeaveBalanceRowDto> AdjustAsync(AdjustLeaveBalanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử điều chỉnh: chính chủ hoặc người quản lý chi nhánh của nhân viên.</summary>
    Task<IReadOnlyList<LeaveBalanceAdjustmentDto>> GetAdjustmentsAsync(int userId, int? year, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quỹ phép năm (UC 5.5). Quỹ được tạo khi cần (duyệt đơn trừ phép đầu tiên hoặc HR điều chỉnh lần đầu) với tổng = số ngày mặc định;
/// trước đó danh sách hiển thị số ngày mặc định. Điều chỉnh: bước 0,5 ngày, tối đa ±30 ngày mỗi lần, tổng mới không nhỏ hơn số ngày
/// đã dùng, chỉ cho năm trước / năm nay / năm sau, không tự điều chỉnh quỹ của mình.
/// </summary>
public class LeaveBalanceService : ILeaveBalanceService
{
    public const decimal MaxAdjustDays = 30;

    private readonly ILeaveBalanceRepository _balances;
    private readonly ILeaveBalanceAdjustmentRepository _adjustments;
    private readonly IUserRepository _users;
    private readonly ISystemSettingRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public LeaveBalanceService(ILeaveBalanceRepository balances, ILeaveBalanceAdjustmentRepository adjustments, IUserRepository users,
        ISystemSettingRepository settings, IUnitOfWork unitOfWork, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _balances = balances;
        _adjustments = adjustments;
        _users = users;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PaginatedResult<LeaveBalanceRowDto>> GetBalancesAsync(LeaveBalanceFilter filter, CancellationToken cancellationToken = default)
    {
        var year = ValidateYear(filter.Year ?? _clock.Today.Year);
        var scope = _currentUser.GetBranchScope(); // null = toàn doanh nghiệp
        var branchId = filter.BranchId ?? scope;
        if (branchId.HasValue) _currentUser.EnsureCanAccessBranch(branchId);

        var defaultDays = await AnnualLeave.DefaultDaysAsync(_settings, cancellationToken);
        var page = await _balances.GetRowsPagedAsync(new LeaveBalanceQuery
        {
            Year = year, BranchId = branchId, Search = filter.Search, PageIndex = filter.PageIndex, PageSize = filter.PageSize
        }, cancellationToken);
        return page.Map(r => ToDto(r, year, defaultDays));
    }

    public async Task<LeaveBalanceRowDto> AdjustAsync(AdjustLeaveBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var me = _currentUser.UserId ?? throw new UnauthorizedException();
        var userId = request.UserId ?? throw new BadRequestException("Vui lòng chọn nhân viên.");
        var year = ValidateYear(request.Year ?? _clock.Today.Year);
        var delta = request.DeltaDays ?? 0;
        var reason = request.Reason?.Trim() ?? string.Empty;

        if (delta == 0) throw new BadRequestException("Số ngày điều chỉnh phải khác 0.");
        if (Math.Abs(delta) > MaxAdjustDays) throw new BadRequestException($"Mỗi lần điều chỉnh tối đa {MaxAdjustDays:0} ngày.");
        if (delta * 2 != Math.Truncate(delta * 2)) throw new BadRequestException("Số ngày điều chỉnh theo bước 0,5 ngày.");
        if (reason.Length == 0) throw new BadRequestException("Vui lòng nhập lý do điều chỉnh.");
        if (userId == me) throw new ForbiddenException("Bạn không thể tự điều chỉnh quỹ phép của mình.");

        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);
        var defaultDays = await AnnualLeave.DefaultDaysAsync(_settings, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _balances.EnsureExistsAsync(user.Id, year, defaultDays, ct);
            if (!await _balances.TryAdjustTotalAsync(user.Id, year, delta, LeaveApprovalService.MaxTotalLeaveDays, ct))
            {
                var balance = await _balances.GetByUserAndYearAsync(user.Id, year, ct);
                throw new BadRequestException(
                    $"Không điều chỉnh được: tổng quỹ phép mới ({Days((balance?.TotalDays ?? 0) + delta)} ngày) phải từ số ngày đã dùng " +
                    $"({Days(balance?.UsedDays ?? 0)}) tới {Days(LeaveApprovalService.MaxTotalLeaveDays)} ngày.");
            }
            await _adjustments.AddAsync(new LeaveBalanceAdjustment
            {
                UserId = user.Id, Year = year, DeltaDays = delta, Reason = reason, CreatedBy = me
            }, ct);
        }, cancellationToken);

        var row = (await _balances.GetRowsPagedAsync(new LeaveBalanceQuery { Year = year, UserId = user.Id }, cancellationToken)).Items.Single();
        return ToDto(row, year, defaultDays);
    }

    public async Task<IReadOnlyList<LeaveBalanceAdjustmentDto>> GetAdjustmentsAsync(int userId, int? year, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);
        if (user.Id != _currentUser.UserId && !RequestAccess.CanManage(_currentUser, user.BranchId))
        {
            throw new ForbiddenException("Bạn không có quyền xem quỹ phép của nhân viên này.");
        }
        var items = await _adjustments.GetByUserAndYearAsync(user.Id, ValidateYear(year ?? _clock.Today.Year), cancellationToken);
        return items.Select(LeaveBalanceAdjustmentDto.From).ToList();
    }

    // ------------------------------------------------------------------

    private static LeaveBalanceRowDto ToDto(LeaveBalanceRow r, int year, decimal defaultDays)
    {
        var isDefault = r.TotalDays is null;
        var total = r.TotalDays ?? defaultDays;
        var used = r.UsedDays ?? 0;
        var remaining = r.RemainingDays ?? defaultDays;
        return new LeaveBalanceRowDto(r.UserId, r.EmployeeCode, r.FullName, r.AvatarUrl, r.BranchId, r.BranchName, year,
            total, used, remaining, r.PendingDays, RequestRules.AvailableLeaveDays(remaining, r.PendingDays), isDefault);
    }

    private int ValidateYear(int year)
    {
        var current = _clock.Today.Year;
        if (year < current - 1 || year > current + 1)
        {
            throw new BadRequestException($"Chỉ xem / điều chỉnh quỹ phép các năm {current - 1}–{current + 1}.");
        }
        return year;
    }

    private static string Days(decimal days) => days.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"));
}
