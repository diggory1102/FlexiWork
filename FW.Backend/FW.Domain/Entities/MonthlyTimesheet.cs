using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Bảng công tháng của một nhân viên (bảng monthly_timesheets): số liệu tổng hợp từ chấm công, lịch và đơn từ theo
/// <see cref="Services.TimesheetCalculator"/>. Chỉ bản <see cref="TimesheetStatus.Draft"/> được tính lại.
/// </summary>
public class MonthlyTimesheet : BaseEntity<long>, IAuditableEntity
{
    public int UserId { get; set; }

    /// <summary>Chi nhánh của nhân viên lúc tính — phạm vi xác nhận của Quản lý.</summary>
    public int? BranchId { get; set; }

    public int Month { get; set; }
    public int Year { get; set; }
    public decimal StandardWorkDays { get; set; }

    public int ScheduledShifts { get; set; }
    public int WorkedShifts { get; set; }
    public int AbsentShifts { get; set; }
    public int PendingLogs { get; set; }
    public int RejectedLogs { get; set; }
    public int MissingCheckouts { get; set; }

    public decimal ActualWorkDays { get; set; }
    public decimal ActualWorkHours { get; set; }
    public int TotalLateMinutes { get; set; }
    public int TotalEarlyMinutes { get; set; }
    public int ViolationCount { get; set; }
    public decimal DeductedWorkDays { get; set; }
    public decimal PenaltyAmount { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal PaidLeaveDays { get; set; }
    public decimal UnpaidLeaveDays { get; set; }
    public int NightShifts { get; set; }
    public int SplitShiftDays { get; set; }

    /// <summary>Lương quy đổi tạm tính — do Task 5.5 điền.</summary>
    public decimal CalculatedSalary { get; set; }

    /// <summary>Giá trị thuộc <see cref="TimesheetStatus"/>.</summary>
    public string Status { get; set; } = TimesheetStatus.Draft;

    public DateTime? CalculatedAt { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? LockedBy { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? Note { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
