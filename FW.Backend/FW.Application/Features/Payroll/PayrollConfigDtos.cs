using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Services;

namespace FW.Application.Features.Payroll;

/// <summary>Khóa trong bảng system_settings dành cho lương.</summary>
public static class PayrollSettingKeys
{
    /// <summary>Hệ số tiền làm thêm giờ (OT) so với đơn giá giờ.</summary>
    public const string OvertimeMultiplier = "overtime_multiplier";
}

// ---------------------------------------------------------------- Tham số chung

/// <param name="StandardWorkDays">Số công chuẩn tháng (chỉnh ở cấu hình chấm công / quy tắc vi phạm).</param>
public record PayrollSettingsDto(decimal OvertimeMultiplier, decimal StandardWorkDays, decimal StandardHoursPerDay);

public class UpdatePayrollSettingsRequest
{
    [Required(ErrorMessage = "Vui lòng nhập hệ số OT.")]
    public decimal? OvertimeMultiplier { get; set; }
}

// ---------------------------------------------------------------- Phụ cấp

public record AllowanceDto(int Id, string Name, string Basis, string BasisLabel, decimal Amount, bool ApplyToAll, bool IsActive, string? Description,
    int AssignedCount)
{
    public static AllowanceDto From(AllowanceRow a) => new(a.Id, a.Name, a.Basis, FW.Domain.Enums.AllowanceBasis.Label(a.Basis), a.Amount, a.ApplyToAll,
        a.IsActive, a.Description, a.AssignedCount);
}

public class AllowanceRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên phụ cấp.")]
    [MaxLength(100, ErrorMessage = "Tên phụ cấp tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>monthly, night_shift, split_day.</summary>
    [Required(ErrorMessage = "Vui lòng chọn cách tính.")]
    public string Basis { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public bool ApplyToAll { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự.")]
    public string? Description { get; set; }
}

// ---------------------------------------------------------------- Lương theo nhân viên

public class PayrollEmployeeFilter : PagedQuery
{
    public int? BranchId { get; set; }

    /// <summary>full_time / part_time.</summary>
    public string? EmploymentType { get; set; }

    /// <summary>true: đã đủ thông tin tính lương; false: còn thiếu.</summary>
    public bool? Configured { get; set; }
}

/// <param name="HourlyRate">Đơn giá giờ riêng của nhân viên (null = theo chức vụ).</param>
/// <param name="EffectiveHourlyRate">Đơn giá giờ đang áp dụng (riêng, nếu chưa có thì theo chức vụ).</param>
/// <param name="OvertimeHourlyRate">Đơn giá giờ dùng tính OT, chưa nhân hệ số OT.</param>
/// <param name="IsConfigured">Toàn thời gian: có lương tháng; bán thời gian: có đơn giá giờ.</param>
public record EmployeePayDto(
    int UserId,
    string EmployeeCode,
    string FullName,
    string? AvatarUrl,
    string Status,
    int? BranchId,
    string? BranchName,
    string? PositionTitle,
    string EmploymentType,
    decimal? BaseSalary,
    decimal? HourlyRate,
    decimal? PositionHourlyRate,
    decimal EffectiveHourlyRate,
    decimal OvertimeHourlyRate,
    bool IsConfigured,
    int AllowanceCount)
{
    public static EmployeePayDto From(PayrollEmployeeRow r, decimal standardWorkDays)
    {
        var effective = PayrollRules.EffectiveHourlyRate(r.HourlyRate, r.PositionHourlyRate);
        return new EmployeePayDto(r.UserId, r.EmployeeCode, r.FullName, r.AvatarUrl, r.Status, r.BranchId, r.BranchName, r.PositionTitle, r.EmploymentType,
            r.BaseSalary, r.HourlyRate, r.PositionHourlyRate, effective,
            PayrollRules.OvertimeHourlyRate(r.EmploymentType, r.BaseSalary, r.HourlyRate, r.PositionHourlyRate, standardWorkDays),
            PayrollRules.IsConfigured(r.EmploymentType, r.BaseSalary, effective), r.AllowanceCount);
    }
}

/// <summary>Một phụ cấp trong danh mục, đối chiếu với nhân viên.</summary>
/// <param name="Assigned">Đang hưởng: áp dụng cho tất cả, hoặc được gán riêng.</param>
/// <param name="OverrideAmount">Mức riêng của nhân viên (null = dùng mức danh mục).</param>
public record EmployeeAllowanceDto(
    int AllowanceId,
    string Name,
    string Basis,
    string BasisLabel,
    decimal DefaultAmount,
    bool ApplyToAll,
    bool IsActive,
    bool Assigned,
    decimal? OverrideAmount,
    decimal EffectiveAmount);

public record EmployeePayDetailDto(EmployeePayDto Pay, IReadOnlyList<EmployeeAllowanceDto> Allowances);

public class UpdateEmployeePayRequest
{
    /// <summary>Lương cơ bản tháng (VNĐ); bỏ trống = xóa.</summary>
    public decimal? BaseSalary { get; set; }

    /// <summary>Đơn giá giờ riêng (VNĐ); bỏ trống = theo chức vụ.</summary>
    public decimal? HourlyRate { get; set; }
}

public class EmployeeAllowanceInput
{
    public int AllowanceId { get; set; }

    /// <summary>Mức riêng; bỏ trống = dùng mức danh mục (không dùng được với phụ cấp áp dụng cho tất cả).</summary>
    public decimal? Amount { get; set; }
}

public class SetEmployeeAllowancesRequest
{
    public List<EmployeeAllowanceInput> Items { get; set; } = new();
}
