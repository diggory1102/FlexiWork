using System.Globalization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Payroll;

public interface IPayrollConfigService
{
    Task<PayrollSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<PayrollSettingsDto> UpdateSettingsAsync(UpdatePayrollSettingsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllowanceDto>> GetAllowancesAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<AllowanceDto> CreateAllowanceAsync(AllowanceRequest request, CancellationToken cancellationToken = default);
    Task<AllowanceDto> UpdateAllowanceAsync(int id, AllowanceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAllowanceAsync(int id, CancellationToken cancellationToken = default);

    Task<PaginatedResult<EmployeePayDto>> GetEmployeesAsync(PayrollEmployeeFilter filter, CancellationToken cancellationToken = default);
    Task<EmployeePayDetailDto> GetEmployeeAsync(int userId, CancellationToken cancellationToken = default);
    Task<EmployeePayDetailDto> UpdateEmployeePayAsync(int userId, UpdateEmployeePayRequest request, CancellationToken cancellationToken = default);
    Task<EmployeePayDetailDto> SetEmployeeAllowancesAsync(int userId, SetEmployeeAllowancesRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cấu hình lương (UC 8.1) — Admin/HR (giới hạn quyền ở Controller): hệ số OT, danh mục phụ cấp, lương cơ bản / đơn giá giờ và phụ cấp riêng
/// của từng nhân viên. Mức tiền là số đồng nguyên, trong giới hạn của <see cref="PayrollRules"/>. Không ai tự sửa lương của mình.
/// Công thức tính lương dùng các số này ở Task 5.5 (xem <see cref="PayrollRules"/>).
/// </summary>
public class PayrollConfigService : IPayrollConfigService
{
    public const int MaxAllowancesPerEmployee = 20;

    private readonly IAllowanceRepository _allowances;
    private readonly IUserAllowanceRepository _userAllowances;
    private readonly IPayrollConfigRepository _payroll;
    private readonly ISystemSettingRepository _settings;
    private readonly IAttendancePolicyService _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PayrollConfigService> _logger;

    public PayrollConfigService(
        IAllowanceRepository allowances,
        IUserAllowanceRepository userAllowances,
        IPayrollConfigRepository payroll,
        ISystemSettingRepository settings,
        IAttendancePolicyService policy,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<PayrollConfigService> logger)
    {
        _allowances = allowances;
        _userAllowances = userAllowances;
        _payroll = payroll;
        _settings = settings;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Tham số chung

    public async Task<PayrollSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        new(await GetMultiplierAsync(cancellationToken), (await _policy.GetAsync(cancellationToken)).StandardWorkDays, PayrollRules.StandardHoursPerDay);

    public async Task<PayrollSettingsDto> UpdateSettingsAsync(UpdatePayrollSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var value = Math.Round(request.OvertimeMultiplier ?? 0, 2, MidpointRounding.AwayFromZero);
        if (!PayrollRules.IsValidOvertimeMultiplier(value))
        {
            throw new BadRequestException(
                $"Hệ số OT phải từ {PayrollRules.MinOvertimeMultiplier:0.#} đến {PayrollRules.MaxOvertimeMultiplier:0.#} lần đơn giá giờ.");
        }

        await _settings.SetValueAsync(PayrollSettingKeys.OvertimeMultiplier, value.ToString(CultureInfo.InvariantCulture),
            "Hệ số tiền làm thêm giờ (OT) so với đơn giá giờ", cancellationToken);
        _logger.LogInformation("Người dùng {UserId} đặt hệ số OT = {Multiplier}", _currentUser.UserId, value);
        return await GetSettingsAsync(cancellationToken);
    }

    private async Task<decimal> GetMultiplierAsync(CancellationToken cancellationToken)
    {
        var raw = await _settings.GetValueAsync(PayrollSettingKeys.OvertimeMultiplier, cancellationToken);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && PayrollRules.IsValidOvertimeMultiplier(value)
            ? value
            : PayrollRules.DefaultOvertimeMultiplier;
    }

    // ------------------------------------------------------------------ Phụ cấp

    public async Task<IReadOnlyList<AllowanceDto>> GetAllowancesAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        (await _allowances.GetListAsync(activeOnly, cancellationToken)).Select(AllowanceDto.From).ToList();

    public async Task<AllowanceDto> CreateAllowanceAsync(AllowanceRequest request, CancellationToken cancellationToken = default)
    {
        var allowance = new Allowance();
        await ApplyAsync(allowance, request, null, cancellationToken);
        var id = await _allowances.AddAsync(allowance, cancellationToken);
        _logger.LogInformation("Người dùng {UserId} tạo phụ cấp {AllowanceId} ({Name})", _currentUser.UserId, id, allowance.Name);
        return AllowanceDto.From(await _allowances.GetRowAsync(id, cancellationToken) ?? throw new NotFoundException("Phụ cấp", id));
    }

    public async Task<AllowanceDto> UpdateAllowanceAsync(int id, AllowanceRequest request, CancellationToken cancellationToken = default)
    {
        var allowance = await _allowances.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Phụ cấp", id);
        var basis = request.Basis.Trim();
        if (basis != allowance.Basis && await _allowances.CountAssignedAsync(id, cancellationToken) > 0)
        {
            throw new BadRequestException("Phụ cấp đã được gán cho nhân viên nên không đổi cách tính được — hãy tạo phụ cấp mới thay thế.");
        }

        await ApplyAsync(allowance, request, id, cancellationToken);
        await _allowances.UpdateAsync(allowance, cancellationToken);
        _logger.LogInformation("Người dùng {UserId} sửa phụ cấp {AllowanceId}", _currentUser.UserId, id);
        return AllowanceDto.From(await _allowances.GetRowAsync(id, cancellationToken) ?? throw new NotFoundException("Phụ cấp", id));
    }

    public async Task DeleteAllowanceAsync(int id, CancellationToken cancellationToken = default)
    {
        _ = await _allowances.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Phụ cấp", id);
        var assigned = await _allowances.CountAssignedAsync(id, cancellationToken);
        if (assigned > 0)
        {
            throw new BadRequestException($"Phụ cấp đang gán cho {assigned} nhân viên — gỡ khỏi nhân viên hoặc chuyển sang \"Ngừng áp dụng\" thay vì xóa.");
        }
        await _allowances.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Người dùng {UserId} xóa phụ cấp {AllowanceId}", _currentUser.UserId, id);
    }

    private async Task ApplyAsync(Allowance allowance, AllowanceRequest request, int? excludeId, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var basis = request.Basis.Trim();
        if (name.Length == 0) throw new BadRequestException("Vui lòng nhập tên phụ cấp.");
        if (!AllowanceBasis.IsValid(basis)) throw new BadRequestException("Cách tính không hợp lệ (monthly, night_shift, split_day).");
        var amount = ValidateMoney(request.Amount, PayrollRules.MaxAllowanceAmount, "Mức phụ cấp", allowZero: false);
        if (await _allowances.NameExistsAsync(name, excludeId, cancellationToken)) throw new BadRequestException($"Đã có phụ cấp \"{name}\".");

        allowance.Name = name;
        allowance.Basis = basis;
        allowance.Amount = amount;
        allowance.ApplyToAll = request.ApplyToAll;
        allowance.IsActive = request.IsActive;
        allowance.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
    }

    // ------------------------------------------------------------------ Theo nhân viên

    public async Task<PaginatedResult<EmployeePayDto>> GetEmployeesAsync(PayrollEmployeeFilter filter, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(filter.EmploymentType) && !EmploymentType.IsValid(filter.EmploymentType))
        {
            throw new BadRequestException("Loại hợp đồng không hợp lệ (full_time, part_time).");
        }

        var standardDays = (await _policy.GetAsync(cancellationToken)).StandardWorkDays;
        var page = await _payroll.GetEmployeesPagedAsync(new PayrollEmployeeQuery
        {
            BranchId = filter.BranchId,
            EmploymentType = string.IsNullOrWhiteSpace(filter.EmploymentType) ? null : filter.EmploymentType,
            Configured = filter.Configured,
            Search = filter.Search,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        }, cancellationToken);
        return page.Map(r => EmployeePayDto.From(r, standardDays));
    }

    public Task<EmployeePayDetailDto> GetEmployeeAsync(int userId, CancellationToken cancellationToken = default) =>
        LoadDetailAsync(userId, cancellationToken);

    public async Task<EmployeePayDetailDto> UpdateEmployeePayAsync(int userId, UpdateEmployeePayRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId);
        var employee = await _payroll.GetEmployeeAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);

        decimal? baseSalary = request.BaseSalary is null ? null : ValidateMoney(request.BaseSalary.Value, PayrollRules.MaxMonthlySalary, "Lương cơ bản", allowZero: false);
        decimal? hourlyRate = request.HourlyRate is null ? null : ValidateRate(request.HourlyRate.Value);
        if (!await _payroll.UpdatePayAsync(userId, baseSalary, hourlyRate, cancellationToken)) throw new NotFoundException("Nhân viên", userId);

        _logger.LogInformation("Người dùng {UserId} cập nhật lương của nhân viên {EmployeeId} ({Type})", _currentUser.UserId, userId, employee.EmploymentType);
        return await LoadDetailAsync(userId, cancellationToken);
    }

    public async Task<EmployeePayDetailDto> SetEmployeeAllowancesAsync(int userId, SetEmployeeAllowancesRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId);
        _ = await _payroll.GetEmployeeAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);

        var items = request.Items ?? new List<EmployeeAllowanceInput>();
        if (items.Count > MaxAllowancesPerEmployee) throw new BadRequestException($"Mỗi nhân viên tối đa {MaxAllowancesPerEmployee} phụ cấp.");
        if (items.GroupBy(i => i.AllowanceId).Any(g => g.Count() > 1)) throw new BadRequestException("Một phụ cấp chỉ được gán một lần.");

        var catalog = (await _allowances.GetListAsync(activeOnly: false, cancellationToken)).ToDictionary(a => a.Id);
        var rows = new List<UserAllowance>();
        foreach (var item in items)
        {
            if (!catalog.TryGetValue(item.AllowanceId, out var allowance)) throw new BadRequestException($"Phụ cấp #{item.AllowanceId} không tồn tại.");
            if (allowance.ApplyToAll && item.Amount is null)
            {
                throw new BadRequestException($"\"{allowance.Name}\" áp dụng cho mọi nhân viên — chỉ cần nhập khi muốn đặt mức riêng.");
            }

            decimal? amount = item.Amount is null
                ? null
                : ValidateMoney(item.Amount.Value, PayrollRules.MaxAllowanceAmount, $"Mức phụ cấp \"{allowance.Name}\"", allowZero: false);
            rows.Add(new UserAllowance { AllowanceId = item.AllowanceId, Amount = amount });
        }

        await _unitOfWork.ExecuteInTransactionAsync(ct => _userAllowances.ReplaceForUserAsync(userId, rows, ct), cancellationToken);
        _logger.LogInformation("Người dùng {UserId} gán {Count} phụ cấp cho nhân viên {EmployeeId}", _currentUser.UserId, rows.Count, userId);
        return await LoadDetailAsync(userId, cancellationToken);
    }

    // ------------------------------------------------------------------

    private async Task<EmployeePayDetailDto> LoadDetailAsync(int userId, CancellationToken cancellationToken)
    {
        var row = await _payroll.GetEmployeeAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);
        var standardDays = (await _policy.GetAsync(cancellationToken)).StandardWorkDays;
        var catalog = await _allowances.GetListAsync(activeOnly: false, cancellationToken);
        var assigned = (await _userAllowances.GetByUserAsync(userId, cancellationToken)).ToDictionary(a => a.AllowanceId);

        var allowances = catalog.Select(a =>
        {
            assigned.TryGetValue(a.Id, out var own);
            var isAssigned = a.ApplyToAll || own is not null;
            return new EmployeeAllowanceDto(a.Id, a.Name, a.Basis, AllowanceBasis.Label(a.Basis), a.Amount, a.ApplyToAll, a.IsActive, isAssigned, own?.Amount,
                PayrollRules.AllowanceUnitAmount(a.Amount, own?.Amount));
        }).ToList();
        return new EmployeePayDetailDto(EmployeePayDto.From(row, standardDays), allowances);
    }

    private void EnsureNotSelf(int userId)
    {
        if (userId == _currentUser.UserId) throw new ForbiddenException("Bạn không thể tự chỉnh lương của mình — nhờ Admin / HR khác.");
    }

    /// <summary>Số tiền (VNĐ) là số đồng nguyên, trong giới hạn.</summary>
    private static decimal ValidateMoney(decimal value, decimal max, string label, bool allowZero)
    {
        if (value != Math.Truncate(value)) throw new BadRequestException($"{label} phải là số đồng nguyên.");
        if (value < 0 || (!allowZero && value == 0)) throw new BadRequestException($"{label} phải lớn hơn 0.");
        if (value > max) throw new BadRequestException($"{label} tối đa {max.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đồng.");
        return value;
    }

    private static decimal ValidateRate(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded <= 0) throw new BadRequestException("Đơn giá giờ phải lớn hơn 0.");
        if (rounded > PayrollRules.MaxHourlyRate)
        {
            throw new BadRequestException($"Đơn giá giờ tối đa {PayrollRules.MaxHourlyRate.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đồng.");
        }
        return rounded;
    }
}
