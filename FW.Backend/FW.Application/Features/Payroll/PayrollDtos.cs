using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Features.Payroll;

// ---------------------------------------------------------------- Yêu cầu / bộ lọc

public class CalculatePayrollRequest
{
    [Range(2000, 2100, ErrorMessage = "Năm không hợp lệ.")]
    public int Year { get; set; }

    [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12.")]
    public int Month { get; set; }
}

public class SendPayslipsRequest
{
    /// <summary>Chỉ gửi các phiếu này (kể cả gửi lại). Bỏ trống = gửi mọi phiếu chưa gửi của kỳ.</summary>
    public List<long>? PayslipIds { get; set; }
}

public class PayrollPeriodFilter
{
    /// <summary>Bỏ trống = mọi năm.</summary>
    public int? Year { get; set; }
}

public class PayslipFilter : PagedQuery
{
    public int? BranchId { get; set; }

    /// <summary>draft (chưa gửi email) / sent (đã gửi).</summary>
    public string? Status { get; set; }
}

public class MyPayslipFilter : PagedQuery
{
    public int? Year { get; set; }
}

// ---------------------------------------------------------------- Kỳ lương

public record PayrollPeriodDto(
    int Id,
    int Month,
    int Year,
    string Status,
    decimal StandardWorkDays,
    decimal OvertimeMultiplier,
    int EmployeeCount,
    decimal TotalNet,
    DateTime? CalculatedAt,
    string? CreatedByName,
    string? FinalizedByName,
    DateTime? FinalizedAt,
    int PayslipCount,
    int SentCount,
    string? Note)
{
    public static PayrollPeriodDto From(PayrollPeriodRow r) => new(r.Id, r.Month, r.Year, r.Status, r.StandardWorkDays, r.OvertimeMultiplier, r.EmployeeCount,
        r.TotalNet, r.CalculatedAt, r.CreatedByName, r.FinalizedByName, r.FinalizedAt, r.PayslipCount, r.SentCount, r.Note);
}

public record PayslipTotalsDto(int Count, decimal Basic, decimal Allowance, decimal Overtime, decimal Penalty, decimal Net)
{
    public static PayslipTotalsDto From(PayslipTotals t) => new(t.Count, t.Basic, t.Allowance, t.Overtime, t.Penalty, t.Net);
}

/// <param name="UnlockedTimesheets">Bảng công của tháng chưa khóa (nháp / đã xác nhận) — chưa được tính lương.</param>
/// <param name="MissingPayslips">Bảng công đã khóa nhưng chưa có phiếu lương (chưa tính lại, hoặc nhân viên chưa cấu hình lương).</param>
public record PayrollReadinessDto(bool MonthEnded, int LockedTimesheets, int UnlockedTimesheets, int MissingPayslips, bool CanFinalize,
    IReadOnlyList<string> Problems);

public record PayrollPeriodDetailDto(PayrollPeriodDto Period, PayslipTotalsDto Totals, PayrollReadinessDto Readiness);

public record PayrollSkippedDto(int UserId, string EmployeeCode, string FullName, string Reason);

public record CalculatePayrollResult(PayrollPeriodDto Period, int Calculated, int UnlockedTimesheets, IReadOnlyList<PayrollSkippedDto> Skipped, string Message);

public record FinalizePayrollResult(PayrollPeriodDto Period, int Payslips, decimal TotalNet, string Message);

public record SendPayslipsResult(int Sent, int Failed, IReadOnlyList<PayrollSkippedDto> Failures, string Message);

// ---------------------------------------------------------------- Phiếu lương

public record PayslipEmployeeDto(int Id, string EmployeeCode, string FullName, string? AvatarUrl, string? PositionTitle);

public record PayslipDto(
    long Id,
    int PeriodId,
    int Month,
    int Year,
    string PeriodStatus,
    PayslipEmployeeDto Employee,
    int? BranchId,
    string? BranchName,
    string EmploymentType,
    decimal BaseSalary,
    decimal HourlyRate,
    decimal StandardWorkDays,
    decimal WorkDays,
    decimal WorkHours,
    decimal OvertimeHours,
    decimal BasicPay,
    decimal AllowanceTotal,
    decimal OvertimePay,
    decimal PenaltyTotal,
    decimal NetPay,
    string Status,
    DateTime? SentAt)
{
    public static PayslipDto From(PayslipRow r) => new(r.Id, r.PeriodId, r.Month, r.Year, r.PeriodStatus,
        new PayslipEmployeeDto(r.UserId, r.EmployeeCode, r.FullName, r.AvatarUrl, r.PositionTitle), r.BranchId, r.BranchName, r.EmploymentType, r.BaseSalary,
        r.HourlyRate, r.StandardWorkDays, r.WorkDays, r.WorkHours, r.OvertimeHours, r.BasicPay, r.AllowanceTotal, r.OvertimePay, r.PenaltyTotal, r.NetPay,
        r.Status, r.SentAt);
}

/// <param name="Amount">Thành tiền; khoản trừ là số âm.</param>
public record PayslipItemDto(string Kind, string Name, decimal Quantity, decimal UnitAmount, decimal Amount, string? Note)
{
    public static PayslipItemDto From(PayslipItem i) => new(i.Kind, i.Name, i.Quantity, i.UnitAmount, i.Amount, i.Note);
}

public record PayslipDetailDto(PayslipDto Payslip, IReadOnlyList<PayslipItemDto> Items);
