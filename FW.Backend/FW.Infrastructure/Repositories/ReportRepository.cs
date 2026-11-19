using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

/// <summary>Truy vấn chỉ đọc cho dashboard và báo cáo (Task 5.6). Kế thừa BaseRepository chỉ để dùng chung phiên / giao dịch.</summary>
public class ReportRepository : BaseRepository<Schedule, long>, IReportRepository
{
    public ReportRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "schedules";

    public Task<IReadOnlyList<ShiftDayRow>> GetShiftDaysAsync(DateTime fromDate, DateTime toDate, int? branchId, int? userId = null,
        CancellationToken cancellationToken = default) =>
        QueryAsync<ShiftDayRow>("""
            SELECT s.`id` AS ScheduleId, s.`user_id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName,
                   s.`branch_id` AS BranchId, b.`name` AS BranchName, s.`work_date` AS WorkDate,
                   sh.`name` AS ShiftName, sh.`start_time` AS StartTime, sh.`end_time` AS EndTime,
                   sh.`late_grace_minutes` AS LateGraceMinutes, sh.`early_leave_grace_minutes` AS EarlyLeaveGraceMinutes,
                   (s.`status` = 'on_leave') AS OnLeave,
                   l.`id` AS LogId, l.`status` AS LogStatus, l.`checkin_time` AS CheckinTime, l.`checkout_time` AS CheckoutTime,
                   COALESCE(l.`late_minutes`, 0) AS LateMinutes, COALESCE(l.`early_minutes`, 0) AS EarlyMinutes,
                   EXISTS(SELECT 1 FROM `leave_requests` r
                          WHERE r.`attendance_log_id` = l.`id` AND r.`status` = 'approved' AND r.`is_deleted` = 0) AS Exempted
            FROM `schedules` s
            JOIN `users` u ON u.`id` = s.`user_id`
            JOIN `shifts` sh ON sh.`id` = s.`shift_id`
            LEFT JOIN `branches` b ON b.`id` = s.`branch_id`
            LEFT JOIN `attendance_logs` l ON l.`schedule_id` = s.`id`
            WHERE s.`is_published` = 1 AND s.`status` <> 'cancelled' AND s.`work_date` BETWEEN @From AND @To
              AND (@BranchId IS NULL OR s.`branch_id` = @BranchId) AND (@UserId IS NULL OR s.`user_id` = @UserId)
            ORDER BY s.`work_date`, s.`id`
            """, new { From = fromDate.Date, To = toDate.Date, BranchId = branchId, UserId = userId }, cancellationToken);

    public Task<IReadOnlyList<PendingCountRow>> GetPendingCountsAsync(int? branchId, CancellationToken cancellationToken = default) =>
        QueryAsync<PendingCountRow>("""
            SELECT x.`branch_id` AS BranchId, CAST(SUM(x.`logs`) AS SIGNED) AS Logs, CAST(SUM(x.`requests`) AS SIGNED) AS Requests
            FROM (
                SELECT `branch_id`, COUNT(*) AS `logs`, 0 AS `requests` FROM `attendance_logs`
                WHERE `status` = 'pending_approval' AND (@BranchId IS NULL OR `branch_id` = @BranchId) GROUP BY `branch_id`
                UNION ALL
                SELECT `branch_id`, 0, COUNT(*) FROM `leave_requests`
                WHERE `status` = 'pending' AND `is_deleted` = 0 AND (@BranchId IS NULL OR `branch_id` = @BranchId) GROUP BY `branch_id`
            ) x
            GROUP BY x.`branch_id`
            """, new { BranchId = branchId }, cancellationToken);

    public Task<IReadOnlyList<LeaveBalanceReportRow>> GetLeaveBalancesAsync(int year, int? branchId, decimal defaultDays,
        CancellationToken cancellationToken = default) =>
        QueryAsync<LeaveBalanceReportRow>("""
            SELECT u.`id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`branch_id` AS BranchId, b.`name` AS BranchName,
                   COALESCE(lb.`total_days`, @Default) AS TotalDays, COALESCE(lb.`used_days`, 0) AS UsedDays,
                   COALESCE(lb.`remaining_days`, @Default) AS RemainingDays, (lb.`id` IS NULL) AS IsDefault
            FROM `users` u
            LEFT JOIN `branches` b ON b.`id` = u.`branch_id`
            LEFT JOIN `leave_balances` lb ON lb.`user_id` = u.`id` AND lb.`year` = @Year
            WHERE u.`is_deleted` = 0 AND u.`status` = 'active' AND (@BranchId IS NULL OR u.`branch_id` = @BranchId)
            ORDER BY u.`employee_code`
            """, new { Year = year, BranchId = branchId, Default = defaultDays }, cancellationToken);

    public Task<IReadOnlyList<LeaveTypeUsageRow>> GetLeaveUsageByTypeAsync(int year, int? branchId, CancellationToken cancellationToken = default) =>
        QueryAsync<LeaveTypeUsageRow>("""
            SELECT t.`id` AS LeaveTypeId, t.`name` AS Name, t.`is_paid` AS IsPaid, t.`deducts_balance` AS DeductsBalance,
                   CAST(COALESCE(SUM(CASE WHEN r.`status` = 'approved' THEN 1 ELSE 0 END), 0) AS SIGNED) AS ApprovedRequests,
                   COALESCE(SUM(CASE WHEN r.`status` = 'approved' THEN r.`duration_days` ELSE 0 END), 0) AS ApprovedDays,
                   COALESCE(SUM(CASE WHEN r.`status` = 'pending' THEN r.`duration_days` ELSE 0 END), 0) AS PendingDays
            FROM `leave_types` t
            LEFT JOIN (
                SELECT r0.`leave_type_id`, r0.`status`, r0.`duration_days`
                FROM `leave_requests` r0
                JOIN `users` u ON u.`id` = r0.`user_id`
                WHERE r0.`is_deleted` = 0 AND YEAR(r0.`start_date`) = @Year AND (@BranchId IS NULL OR u.`branch_id` = @BranchId)
            ) r ON r.`leave_type_id` = t.`id`
            WHERE t.`category` = 'leave' AND t.`is_deleted` = 0
            GROUP BY t.`id`, t.`name`, t.`is_paid`, t.`deducts_balance`
            ORDER BY t.`id`
            """, new { Year = year, BranchId = branchId }, cancellationToken);

    public Task<IReadOnlyList<MonthUsageRow>> GetLeaveUsageByMonthAsync(int year, int? branchId, CancellationToken cancellationToken = default) =>
        QueryAsync<MonthUsageRow>("""
            SELECT MONTH(r.`start_date`) AS Month, COALESCE(SUM(r.`duration_days`), 0) AS Days
            FROM `leave_requests` r
            JOIN `leave_types` t ON t.`id` = r.`leave_type_id`
            JOIN `users` u ON u.`id` = r.`user_id`
            WHERE r.`is_deleted` = 0 AND t.`category` = @Category AND r.`status` = @Approved AND YEAR(r.`start_date`) = @Year
              AND (@BranchId IS NULL OR u.`branch_id` = @BranchId)
            GROUP BY MONTH(r.`start_date`)
            ORDER BY Month
            """, new { Year = year, BranchId = branchId, Category = RequestCategory.Leave, Approved = RequestStatus.Approved }, cancellationToken);
}
