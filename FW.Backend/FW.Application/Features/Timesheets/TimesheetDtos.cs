using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Services;

namespace FW.Application.Features.Timesheets;

public class TimesheetFilter : PagedQuery
{
    /// <summary>Mặc định tháng hiện tại.</summary>
    public int? Year { get; set; }

    public int? Month { get; set; }

    /// <summary>Admin/HR lọc chi nhánh; Quản lý luôn là chi nhánh mình.</summary>
    public int? BranchId { get; set; }

    public string? Status { get; set; }
}

public class MyTimesheetFilter
{
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class CalculateTimesheetsRequest
{
    [Range(2000, 2100, ErrorMessage = "Năm không hợp lệ.")]
    public int Year { get; set; }

    [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12.")]
    public int Month { get; set; }

    /// <summary>Admin/HR chọn chi nhánh (bỏ trống = toàn doanh nghiệp); Quản lý luôn là chi nhánh mình.</summary>
    public int? BranchId { get; set; }

    /// <summary>Chỉ xử lý các nhân viên này (bỏ trống = tất cả trong phạm vi).</summary>
    public List<int>? UserIds { get; set; }
}

public class ReopenTimesheetRequest
{
    [Required(ErrorMessage = "Vui lòng ghi lý do mở lại bảng công.")]
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

public record TimesheetEmployeeDto(int Id, string EmployeeCode, string FullName, string? AvatarUrl, string? PositionTitle);

public record TimesheetFiguresDto(
    int ScheduledShifts,
    int WorkedShifts,
    int AbsentShifts,
    int PendingLogs,
    int RejectedLogs,
    int MissingCheckouts,
    decimal ActualWorkDays,
    decimal ActualWorkHours,
    int TotalLateMinutes,
    int TotalEarlyMinutes,
    int ViolationCount,
    decimal DeductedWorkDays,
    decimal PenaltyAmount,
    decimal TotalOvertimeHours,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    int NightShifts,
    int SplitShiftDays)
{
    public static TimesheetFiguresDto From(TimesheetTotals t) => new(t.ScheduledShifts, t.WorkedShifts, t.AbsentShifts, t.PendingLogs, t.RejectedLogs,
        t.MissingCheckouts, t.ActualWorkDays, t.ActualWorkHours, t.TotalLateMinutes, t.TotalEarlyMinutes, t.ViolationCount, t.DeductedWorkDays,
        t.PenaltyAmount, t.OvertimeHours, t.PaidLeaveDays, t.UnpaidLeaveDays, t.NightShifts, t.SplitShiftDays);

    public static TimesheetFiguresDto From(TimesheetRow r) => new(r.ScheduledShifts, r.WorkedShifts, r.AbsentShifts, r.PendingLogs, r.RejectedLogs,
        r.MissingCheckouts, r.ActualWorkDays, r.ActualWorkHours, r.TotalLateMinutes, r.TotalEarlyMinutes, r.ViolationCount, r.DeductedWorkDays,
        r.PenaltyAmount, r.TotalOvertimeHours, r.PaidLeaveDays, r.UnpaidLeaveDays, r.NightShifts, r.SplitShiftDays);
}

/// <param name="IsPreview">true = chưa có bảng công đã lưu (hoặc đang nháp) — số liệu tính trực tiếp từ dữ liệu hiện tại.</param>
public record TimesheetDto(
    long? Id,
    TimesheetEmployeeDto Employee,
    int? BranchId,
    string? BranchName,
    int Year,
    int Month,
    decimal StandardWorkDays,
    string Status,
    bool IsPreview,
    TimesheetFiguresDto Figures,
    decimal CalculatedSalary,
    DateTime? CalculatedAt,
    string? ConfirmedByName,
    DateTime? ConfirmedAt,
    string? LockedByName,
    DateTime? LockedAt,
    string? Note);

public record TimesheetLineDto(
    long ScheduleId,
    DateTime WorkDate,
    string ShiftName,
    string StartTime,
    string EndTime,
    string Outcome,
    string? LeaveKind,
    decimal WorkUnits,
    decimal WorkHours,
    int LateMinutes,
    int EarlyMinutes,
    decimal DeductedUnits,
    decimal Penalty,
    bool Exempted,
    long? LogId,
    DateTime? CheckIn,
    DateTime? CheckOut);

public record OvertimeLineDto(DateTime StartAt, DateTime EndAt, decimal Hours);

/// <param name="Drifted">Bảng công đã xác nhận / khóa nhưng dữ liệu nguồn đã đổi sau đó — số liệu trực tiếp (<c>Live</c>) khác số liệu đã lưu.</param>
public record TimesheetDetailDto(
    TimesheetDto Timesheet,
    TimesheetFiguresDto Live,
    bool Drifted,
    IReadOnlyList<TimesheetLineDto> Lines,
    IReadOnlyList<OvertimeLineDto> Overtime,
    bool CanConfirm,
    bool CanLock,
    bool CanReopen);

public record CalculateTimesheetsResult(int Year, int Month, int Calculated, int SkippedConfirmed, int SkippedFrozen, string Message);

public record ConfirmTimesheetsResult(int Confirmed, IReadOnlyList<TimesheetBlockerDto> Blocked, string Message);

public record LockTimesheetsResult(int Locked, int RemainingDrafts, string Message);

/// <summary>Bảng công chưa xác nhận được và lý do.</summary>
public record TimesheetBlockerDto(int UserId, string FullName, string Reason);

public record TimesheetSummaryDto(
    int Year,
    int Month,
    int Draft,
    int Confirmed,
    int Locked,
    int Finalized,
    bool MonthEnded);
