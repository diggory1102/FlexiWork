using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Domain.Entities;

namespace FW.Application.Features.Scheduling;

public interface IShiftService
{
    Task<PaginatedResult<ShiftDto>> GetPagedAsync(ShiftFilter filter, CancellationToken cancellationToken = default);
    Task<ShiftDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Các ca chi nhánh được phép xếp lịch: ca dùng chung + ca riêng của chi nhánh.</summary>
    Task<IReadOnlyList<ShiftDto>> GetAvailableForBranchAsync(int branchId, CancellationToken cancellationToken = default);

    Task<ShiftDto> CreateAsync(ShiftRequest request, CancellationToken cancellationToken = default);
    Task<ShiftDto> UpdateAsync(int id, ShiftRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Khai báo khung ca (UC 1.3 / UC 3.1).
/// Phân quyền: Admin/HR quản lý mọi ca (dùng chung và ca riêng của bất kỳ chi nhánh nào);
/// Quản lý chi nhánh xem ca dùng chung + ca của chi nhánh mình và chỉ được thêm/sửa/xóa ca riêng của chi nhánh mình.
/// Ca đã được xếp lịch thì không đổi được giờ làm, giờ nghỉ, hệ số công, cờ ca đêm, chi nhánh (giữ đúng lịch sử chấm công).
/// </summary>
public class ShiftService : IShiftService
{
    /// <summary>Độ dài tối đa của một ca (kể cả giờ nghỉ).</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(16);

    private const string InUseMessage =
        "Ca đã được xếp lịch nên không thể đổi giờ làm, giờ nghỉ, hệ số công, ca đêm hoặc chi nhánh. Hãy tạo ca mới cho khung giờ mới.";

    private readonly IShiftRepository _shifts;
    private readonly IBranchRepository _branches;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ShiftService(IShiftRepository shifts, IBranchRepository branches, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _shifts = shifts;
        _branches = branches;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    // Truy vấn
    // ------------------------------------------------------------------

    public async Task<PaginatedResult<ShiftDto>> GetPagedAsync(ShiftFilter filter, CancellationToken cancellationToken = default)
    {
        // Quản lý chỉ thấy ca dùng chung + ca của chi nhánh mình, bất kể tham số lọc gửi lên
        var scope = _currentUser.GetBranchScope();
        if (scope.HasValue) filter.BranchId = scope;

        var page = await _shifts.GetPagedAsync(filter, cancellationToken);
        return page.Map(ToDto);
    }

    public async Task<ShiftDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var shift = await GetReadableAsync(id, cancellationToken);
        return new ShiftDetailDto(ToDto(shift), await _shifts.IsInUseAsync(id, cancellationToken));
    }

    public async Task<IReadOnlyList<ShiftDto>> GetAvailableForBranchAsync(int branchId, CancellationToken cancellationToken = default)
    {
        _currentUser.EnsureCanAccessBranch(branchId);
        _ = await _branches.GetByIdAsync(branchId, cancellationToken) ?? throw new NotFoundException("Chi nhánh", branchId);

        var shifts = await _shifts.GetAvailableForBranchAsync(branchId, cancellationToken);
        return shifts.Select(ToDto).ToList();
    }

    // ------------------------------------------------------------------
    // Thêm / sửa / xóa
    // ------------------------------------------------------------------

    public async Task<ShiftDto> CreateAsync(ShiftRequest request, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.ShiftCode);
        if (await _shifts.ExistsByCodeAsync(code, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Mã ca '{code}' đã tồn tại.");
        }

        var shift = new Shift { ShiftCode = code };
        Apply(shift, request, await ResolveBranchAsync(request.BranchId, cancellationToken));

        await _shifts.AddAsync(shift, cancellationToken);
        return ToDto((await _shifts.GetByIdAsync(shift.Id, cancellationToken))!);
    }

    public async Task<ShiftDto> UpdateAsync(int id, ShiftRequest request, CancellationToken cancellationToken = default)
    {
        var shift = await GetEditableAsync(id, cancellationToken);

        var code = NormalizeCode(request.ShiftCode);
        if (await _shifts.ExistsByCodeAsync(code, excludeId: id, cancellationToken))
        {
            throw new BadRequestException($"Mã ca '{code}' đã tồn tại.");
        }

        var before = (shift.StartTime, shift.EndTime, shift.BreakMinutes, shift.WorkUnits, shift.IsNightShift, shift.BranchId);
        shift.ShiftCode = code;
        Apply(shift, request, await ResolveBranchAsync(request.BranchId, cancellationToken));

        var after = (shift.StartTime, shift.EndTime, shift.BreakMinutes, shift.WorkUnits, shift.IsNightShift, shift.BranchId);
        if (before != after && await _shifts.IsInUseAsync(id, cancellationToken))
        {
            throw new BadRequestException(InUseMessage);
        }

        await _shifts.UpdateAsync(shift, cancellationToken);
        return ToDto((await _shifts.GetByIdAsync(id, cancellationToken))!);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await GetEditableAsync(id, cancellationToken);

        // Lịch đã qua vẫn tham chiếu được ca (xóa mềm); chỉ chặn khi còn lịch sắp tới
        if (await _shifts.HasActiveSchedulesFromAsync(id, _clock.Today.ToDateTime(TimeOnly.MinValue), cancellationToken))
        {
            throw new BadRequestException("Không thể xóa: ca đang có lịch làm việc từ hôm nay trở đi. Hãy hủy hoặc chuyển các lịch đó sang ca khác trước.");
        }

        await _shifts.DeleteAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Hàm hỗ trợ
    // ------------------------------------------------------------------

    private bool CanEdit(Shift shift) =>
        _currentUser.HasCompanyWideAccess() || (shift.BranchId.HasValue && _currentUser.CanAccessBranch(shift.BranchId));

    private ShiftDto ToDto(Shift shift) => ShiftDto.From(shift, CanEdit(shift));

    private async Task<Shift> GetReadableAsync(int id, CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Ca làm việc", id);
        if (shift.BranchId.HasValue) _currentUser.EnsureCanAccessBranch(shift.BranchId);
        else _currentUser.GetBranchScope(); // ca dùng chung: mọi vai trò quản lý đều xem được
        return shift;
    }

    private async Task<Shift> GetEditableAsync(int id, CancellationToken cancellationToken)
    {
        var shift = await GetReadableAsync(id, cancellationToken);
        if (!CanEdit(shift))
        {
            throw new ForbiddenException("Ca dùng chung chỉ Quản trị viên hoặc Nhân sự được sửa. Quản lý chi nhánh hãy tạo ca riêng cho chi nhánh mình.");
        }
        return shift;
    }

    /// <summary>
    /// Chi nhánh sở hữu ca: Admin/HR chọn tự do (null = ca dùng chung); Quản lý luôn là chi nhánh của mình.
    /// </summary>
    private async Task<int?> ResolveBranchAsync(int? requested, CancellationToken cancellationToken)
    {
        var branchId = requested;
        if (!_currentUser.HasCompanyWideAccess())
        {
            branchId ??= _currentUser.GetBranchScope();
            _currentUser.EnsureCanAccessBranch(branchId);
        }

        if (branchId.HasValue && await _branches.GetByIdAsync(branchId.Value, cancellationToken) == null)
        {
            throw new BadRequestException("Chi nhánh không tồn tại.");
        }
        return branchId;
    }

    private static void Apply(Shift shift, ShiftRequest request, int? branchId)
    {
        if (!ShiftTime.TryParse(request.StartTime, out var start))
            throw new BadRequestException("Giờ bắt đầu không hợp lệ (định dạng HH:mm, VD 08:00).");
        if (!ShiftTime.TryParse(request.EndTime, out var end))
            throw new BadRequestException("Giờ kết thúc không hợp lệ (định dạng HH:mm, VD 17:30).");
        if (start == end)
            throw new BadRequestException("Giờ kết thúc phải khác giờ bắt đầu.");

        shift.StartTime = start;
        shift.EndTime = end;
        shift.IsCrossDay = Shift.SpansMidnight(start, end);
        shift.BreakMinutes = request.BreakMinutes;

        if (shift.Duration > MaxDuration)
            throw new BadRequestException($"Ca làm việc dài tối đa {MaxDuration.TotalHours:0} giờ (kể cả giờ nghỉ).");
        if (shift.WorkingMinutes < 30)
            throw new BadRequestException("Giờ nghỉ quá dài: thời gian làm việc thực tế của ca phải từ 30 phút trở lên.");
        if (request.LateGraceMinutes >= shift.WorkingMinutes || request.EarlyLeaveGraceMinutes >= shift.WorkingMinutes)
            throw new BadRequestException("Số phút cho phép đi muộn / về sớm phải nhỏ hơn thời gian làm việc của ca.");

        shift.Name = request.Name.Trim();
        shift.BranchId = branchId;
        shift.WorkUnits = Math.Round(request.WorkUnits, 2);
        shift.IsNightShift = request.IsNightShift;
        shift.LateGraceMinutes = request.LateGraceMinutes;
        shift.EarlyLeaveGraceMinutes = request.EarlyLeaveGraceMinutes;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
