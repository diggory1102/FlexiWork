using FW.Application.Common.Models;

namespace FW.Application.Features.Reports;

// ---------------------------------------------------------------- Bộ lọc

public class DashboardFilter
{
    /// <summary>Admin/HR: bỏ trống = mọi chi nhánh; Quản lý luôn là chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    /// <summary>Ngày xem (mặc định hôm nay).</summary>
    public DateOnly? Date { get; set; }
}

public class ViolationReportFilter : PagedQuery
{
    public int? BranchId { get; set; }

    /// <summary>Mặc định đầu tháng này.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Mặc định hôm nay.</summary>
    public DateOnly? ToDate { get; set; }
}

public class EmployeeViolationFilter
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class LeaveUsageFilter
{
    /// <summary>Mặc định năm nay.</summary>
    public int? Year { get; set; }

    public int? BranchId { get; set; }
}

// ---------------------------------------------------------------- Dashboard

/// <summary>
/// Quân số và chấm công của một ngày. Số "nhân viên" đếm không trùng; số "ca" đếm từng ca đã xếp.
/// </summary>
/// <param name="CheckedInEmployees">Đã chấm công (hợp lệ hoặc chờ duyệt).</param>
/// <param name="WorkingEmployees">Đang trong ca: đã check-in, chưa check-out.</param>
/// <param name="WaitingCheckInShifts">Ca đã bắt đầu, chưa kết thúc mà chưa có lượt chấm công.</param>
/// <param name="AbsentShifts">Ca đã kết thúc mà không chấm công, không nghỉ phép.</param>
/// <param name="LateShifts">Ca đi muộn quá số phút cho phép.</param>
/// <param name="OnTimeRate">% lượt có mặt không đi muộn; null nếu chưa ai chấm công.</param>
public record HeadcountDto(
    int ScheduledEmployees,
    int ScheduledShifts,
    int CheckedInEmployees,
    int WorkingEmployees,
    int WaitingCheckInShifts,
    int UpcomingShifts,
    int CompletedShifts,
    int AbsentShifts,
    int OnLeaveEmployees,
    int LateShifts,
    decimal? OnTimeRate,
    int PendingAttendance,
    int PendingRequests);

public record BranchHeadcountDto(int BranchId, string BranchName, HeadcountDto Headcount);

/// <summary>Một ngày trong biểu đồ xu hướng.</summary>
public record DashboardTrendDto(DateTime Date, int ScheduledShifts, int AttendedShifts, int LateShifts, int AbsentShifts, decimal? OnTimeRate);

/// <param name="AsOf">Thời điểm tính (giờ Việt Nam) — giao diện làm mới định kỳ để có số liệu gần thời gian thực.</param>
/// <param name="Totals">Tổng hợp mọi chi nhánh đang xem.</param>
public record DashboardDto(
    DateTime AsOf,
    DateTime Date,
    HeadcountDto Totals,
    IReadOnlyList<BranchHeadcountDto> Branches,
    IReadOnlyList<DashboardTrendDto> Trend);

// ---------------------------------------------------------------- Báo cáo vi phạm

/// <param name="EvaluatedShifts">Ca đã có kết quả: có mặt hoặc vắng mặt (không tính nghỉ phép, ca chưa tới, lượt bị từ chối).</param>
/// <param name="ViolatingShifts">Ca có đi muộn, về sớm hoặc vắng mặt.</param>
/// <param name="ViolationRate">% ca vi phạm trên số ca đã có kết quả; null nếu chưa có ca nào.</param>
public record ViolationSummaryDto(
    int ScheduledShifts,
    int EvaluatedShifts,
    int AttendedShifts,
    int OnLeaveShifts,
    int LateCount,
    int LateMinutes,
    int EarlyCount,
    int EarlyMinutes,
    int AbsentCount,
    int ViolatingShifts,
    decimal? ViolationRate,
    decimal PenaltyAmount,
    decimal DeductedWorkDays);

public record BranchViolationDto(int BranchId, string BranchName, ViolationSummaryDto Summary);

public record EmployeeViolationDto(
    int UserId,
    string EmployeeCode,
    string FullName,
    int BranchId,
    string? BranchName,
    ViolationSummaryDto Summary);

public record ViolationReportDto(
    DateTime FromDate,
    DateTime ToDate,
    ViolationSummaryDto Summary,
    IReadOnlyList<BranchViolationDto> Branches,
    PaginatedResult<EmployeeViolationDto> Employees);

/// <param name="Type">late, early_leave hoặc absent.</param>
public record ViolationItemDto(
    DateTime WorkDate,
    string ShiftName,
    string StartTime,
    string EndTime,
    string Type,
    int Minutes,
    decimal PenaltyAmount,
    decimal DeductWorkUnits);

public record EmployeeViolationDetailDto(
    EmployeeViolationDto Employee,
    DateTime FromDate,
    DateTime ToDate,
    IReadOnlyList<ViolationItemDto> Items);

// ---------------------------------------------------------------- Báo cáo quỹ phép

public record LeaveUsageEmployeeDto(
    int UserId,
    string EmployeeCode,
    string FullName,
    string? BranchName,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays,
    bool IsDefault);

/// <param name="UsageRate">% quỹ phép đã dùng trên tổng quỹ; null nếu tổng quỹ bằng 0.</param>
/// <param name="PendingDays">Số ngày phép năm đang chờ duyệt (sẽ trừ vào quỹ khi duyệt).</param>
/// <param name="WithoutBalance">Số nhân viên chưa khởi tạo quỹ riêng (đang dùng mức mặc định).</param>
/// <param name="LowBalanceEmployees">Số nhân viên còn ít phép (từ 0 đến mức cảnh báo).</param>
public record LeaveUsageSummaryDto(
    int Employees,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays,
    decimal? UsageRate,
    decimal PendingDays,
    int WithoutBalance,
    int LowBalanceEmployees);

public record BranchLeaveUsageDto(int? BranchId, string BranchName, int Employees, decimal TotalDays, decimal UsedDays, decimal RemainingDays, decimal? UsageRate);

public record LeaveTypeUsageDto(int LeaveTypeId, string Name, bool IsPaid, bool DeductsBalance, int ApprovedRequests, decimal ApprovedDays, decimal PendingDays);

public record MonthUsageDto(int Month, decimal Days);

public record LeaveUsageReportDto(
    int Year,
    decimal DefaultDays,
    decimal LowBalanceDays,
    LeaveUsageSummaryDto Summary,
    IReadOnlyList<BranchLeaveUsageDto> Branches,
    IReadOnlyList<LeaveTypeUsageDto> ByType,
    IReadOnlyList<MonthUsageDto> ByMonth,
    IReadOnlyList<LeaveUsageEmployeeDto> TopUsed,
    IReadOnlyList<LeaveUsageEmployeeDto> LowRemaining);
