namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>Một ca đã công bố kèm lượt chấm công của ca đó (nếu có) — dữ liệu thô cho dashboard và báo cáo vi phạm.</summary>
public class ShiftDayRow
{
    public long ScheduleId { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int LateGraceMinutes { get; set; }
    public int EarlyLeaveGraceMinutes { get; set; }

    /// <summary>Lịch đã được đánh dấu nghỉ phép (đơn đã duyệt).</summary>
    public bool OnLeave { get; set; }

    public long? LogId { get; set; }
    public string? LogStatus { get; set; }
    public DateTime? CheckinTime { get; set; }
    public DateTime? CheckoutTime { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyMinutes { get; set; }

    /// <summary>Đơn giải trình cho lượt chấm công này đã được duyệt.</summary>
    public bool Exempted { get; set; }

    public bool SpansMidnight => EndTime <= StartTime;
    public TimeSpan Duration => SpansMidnight ? EndTime + TimeSpan.FromDays(1) - StartTime : EndTime - StartTime;
    public DateTime StartAt => WorkDate.Date + StartTime;
    public DateTime EndAt => StartAt + Duration;
}

/// <summary>Số việc đang chờ duyệt của một chi nhánh (<see cref="BranchId"/> null = đơn chưa gán chi nhánh).</summary>
public class PendingCountRow
{
    public int? BranchId { get; set; }
    public int Logs { get; set; }
    public int Requests { get; set; }
}

public class LeaveBalanceReportRow
{
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays { get; set; }

    /// <summary>Chưa khởi tạo quỹ phép riêng — số liệu là mức mặc định.</summary>
    public bool IsDefault { get; set; }
}

public class LeaveTypeUsageRow
{
    public int LeaveTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public bool DeductsBalance { get; set; }
    public int ApprovedRequests { get; set; }
    public decimal ApprovedDays { get; set; }
    public decimal PendingDays { get; set; }
}

public class MonthUsageRow
{
    public int Month { get; set; }
    public decimal Days { get; set; }
}

/// <summary>Truy vấn dành riêng cho dashboard và báo cáo (Task 5.6) — chỉ đọc.</summary>
public interface IReportRepository
{
    /// <summary>
    /// Ca đã công bố (chưa hủy) trong khoảng ngày kèm lượt chấm công; <paramref name="branchId"/> null = mọi chi nhánh,
    /// <paramref name="userId"/> null = mọi nhân viên.
    /// </summary>
    Task<IReadOnlyList<ShiftDayRow>> GetShiftDaysAsync(DateTime fromDate, DateTime toDate, int? branchId, int? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lượt chấm công chờ duyệt và đơn chờ duyệt, gộp theo chi nhánh.</summary>
    Task<IReadOnlyList<PendingCountRow>> GetPendingCountsAsync(int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Quỹ phép năm của nhân viên đang hoạt động (chưa có quỹ riêng thì dùng <paramref name="defaultDays"/>).</summary>
    Task<IReadOnlyList<LeaveBalanceReportRow>> GetLeaveBalancesAsync(int year, int? branchId, decimal defaultDays, CancellationToken cancellationToken = default);

    /// <summary>Số ngày nghỉ theo từng loại đơn nghỉ (đã duyệt / đang chờ) của năm — tính theo năm của ngày bắt đầu.</summary>
    Task<IReadOnlyList<LeaveTypeUsageRow>> GetLeaveUsageByTypeAsync(int year, int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Số ngày nghỉ đã duyệt theo tháng (theo tháng của ngày bắt đầu).</summary>
    Task<IReadOnlyList<MonthUsageRow>> GetLeaveUsageByMonthAsync(int year, int? branchId, CancellationToken cancellationToken = default);
}
