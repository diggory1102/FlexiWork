using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class TimesheetRepository : BaseRepository<MonthlyTimesheet, long>, ITimesheetRepository
{
    public TimesheetRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "monthly_timesheets";

    static TimesheetRepository() => DapperConfig.Configure();

    private const string DetailSelect = """
        SELECT t.*, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`avatar_url` AS AvatarUrl, p.`title` AS PositionTitle,
               b.`name` AS BranchName, cu.`full_name` AS ConfirmedByName, lu.`full_name` AS LockedByName
        FROM `monthly_timesheets` t
        JOIN `users` u ON u.`id` = t.`user_id`
        LEFT JOIN `positions` p ON p.`id` = u.`position_id`
        LEFT JOIN `branches` b ON b.`id` = t.`branch_id`
        LEFT JOIN `users` cu ON cu.`id` = t.`confirmed_by`
        LEFT JOIN `users` lu ON lu.`id` = t.`locked_by`
        """;

    public async Task<PaginatedResult<TimesheetRow>> GetDetailsPagedAsync(TimesheetQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "t.`year` = @Year", "t.`month` = @Month" };
        var parameters = new DynamicParameters(new { query.Year, query.Month });
        if (query.BranchId.HasValue)
        {
            conditions.Add("t.`branch_id` = @BranchId");
            parameters.Add("BranchId", query.BranchId);
        }
        if (query.UserId.HasValue)
        {
            conditions.Add("t.`user_id` = @UserId");
            parameters.Add("UserId", query.UserId);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            conditions.Add("t.`status` = @Status");
            parameters.Add("Status", query.Status);
        }
        AddSearchCondition(query.Search, new[] { "u.`full_name`", "u.`employee_code`" }, conditions, parameters);
        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT COUNT(*) FROM `monthly_timesheets` t JOIN `users` u ON u.`id` = t.`user_id` WHERE {where};
            {DetailSelect} WHERE {where} ORDER BY b.`name`, u.`employee_code` LIMIT @Take OFFSET @Skip;
            """;
        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<TimesheetRow>()).ToList();
        return PaginatedResult<TimesheetRow>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    public Task<TimesheetRow?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<TimesheetRow>($"{DetailSelect} WHERE t.`id` = @Id", new { Id = id }, cancellationToken);

    public Task<IReadOnlyList<MonthlyTimesheet>> GetByUsersAsync(int year, int month, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return Task.FromResult<IReadOnlyList<MonthlyTimesheet>>(Array.Empty<MonthlyTimesheet>());
        return QueryAsync<MonthlyTimesheet>($"{SelectFrom} WHERE `year` = @Year AND `month` = @Month AND `user_id` IN @Ids",
            new { Year = year, Month = month, Ids = userIds }, cancellationToken);
    }

    // Cột số liệu do hệ thống tính (khác với khóa, trạng thái và ghi chú)
    private static readonly string[] Figures =
    {
        "branch_id", "standard_work_days", "scheduled_shifts", "worked_shifts", "absent_shifts", "pending_logs", "rejected_logs", "missing_checkouts",
        "actual_work_days", "actual_work_hours", "total_late_minutes", "total_early_minutes", "violation_count", "deducted_work_days", "penalty_amount",
        "total_overtime_hours", "paid_leave_days", "unpaid_leave_days", "night_shifts", "split_shift_days", "calculated_at"
    };

    private static readonly string UpsertSql = BuildUpsertSql();

    private static string BuildUpsertSql()
    {
        var columns = new[] { "user_id", "month", "year", "status" }.Concat(Figures).ToArray();
        var values = string.Join(", ", columns.Select(c => "@" + ToParam(c)));
        // VALUES(col) = giá trị định ghi; chỉ ghi đè khi bảng công hiện tại vẫn là nháp
        var updates = string.Join(", ", Figures.Select(c => $"`{c}` = IF(`status` = 'draft', VALUES(`{c}`), `{c}`)"));
        return $"INSERT INTO `monthly_timesheets` ({string.Join(", ", columns.Select(c => $"`{c}`"))}) VALUES ({values}) ON DUPLICATE KEY UPDATE {updates}";
    }

    private static string ToParam(string column) => string.Concat(column.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    public Task UpsertDraftAsync(MonthlyTimesheet t, CancellationToken cancellationToken = default) =>
        ExecuteAsync(UpsertSql, new
        {
            t.UserId, t.Month, t.Year, Status = TimesheetStatus.Draft,
            t.BranchId, t.StandardWorkDays, t.ScheduledShifts, t.WorkedShifts, t.AbsentShifts, t.PendingLogs, t.RejectedLogs, t.MissingCheckouts,
            t.ActualWorkDays, t.ActualWorkHours, t.TotalLateMinutes, t.TotalEarlyMinutes, t.ViolationCount, t.DeductedWorkDays, t.PenaltyAmount,
            t.TotalOvertimeHours, t.PaidLeaveDays, t.UnpaidLeaveDays, t.NightShifts, t.SplitShiftDays, t.CalculatedAt
        }, cancellationToken);

    public async Task<bool> TryConfirmAsync(long id, int confirmedBy, DateTime confirmedAt, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("""
            UPDATE `monthly_timesheets` SET `status` = @Confirmed, `confirmed_by` = @By, `confirmed_at` = @At
            WHERE `id` = @Id AND `status` = @Draft
            """, new { Id = id, By = confirmedBy, At = confirmedAt, Confirmed = TimesheetStatus.Confirmed, Draft = TimesheetStatus.Draft }, cancellationToken) == 1;

    public Task<int> LockConfirmedAsync(int year, int month, int? branchId, IReadOnlyCollection<int>? userIds, int lockedBy, DateTime lockedAt,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync($"""
            UPDATE `monthly_timesheets` SET `status` = @Locked, `locked_by` = @By, `locked_at` = @At
            WHERE `year` = @Year AND `month` = @Month AND `status` = @Confirmed
              AND (@BranchId IS NULL OR `branch_id` = @BranchId){(userIds is { Count: > 0 } ? " AND `user_id` IN @Ids" : string.Empty)}
            """, new
        {
            Year = year, Month = month, BranchId = branchId, Ids = userIds, By = lockedBy, At = lockedAt,
            Locked = TimesheetStatus.Locked, Confirmed = TimesheetStatus.Confirmed
        }, cancellationToken);

    public async Task<bool> TryReopenAsync(long id, string fromStatus, string? note, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("""
            UPDATE `monthly_timesheets`
            SET `status` = @Draft, `confirmed_by` = NULL, `confirmed_at` = NULL, `locked_by` = NULL, `locked_at` = NULL, `note` = @Note
            WHERE `id` = @Id AND `status` = @From
            """, new { Id = id, From = fromStatus, Note = note, Draft = TimesheetStatus.Draft }, cancellationToken) == 1;

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(int year, int month, int? branchId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<(string Status, long Total)>("""
            SELECT `status` AS Status, COUNT(*) AS Total FROM `monthly_timesheets`
            WHERE `year` = @Year AND `month` = @Month AND (@BranchId IS NULL OR `branch_id` = @BranchId) GROUP BY `status`
            """, new { Year = year, Month = month, BranchId = branchId }, cancellationToken);
        return rows.ToDictionary(r => r.Status, r => (int)r.Total);
    }

    public Task<bool> IsFrozenAsync(int userId, int year, int month, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM `monthly_timesheets`
                          WHERE `user_id` = @UserId AND `year` = @Year AND `month` = @Month AND `status` IN @Frozen)
            """, new { UserId = userId, Year = year, Month = month, Frozen = new[] { TimesheetStatus.Locked, TimesheetStatus.Finalized } }, cancellationToken);

    public Task<IReadOnlyList<MonthlyTimesheet>> GetByMonthAsync(int year, int month, CancellationToken cancellationToken = default) =>
        QueryAsync<MonthlyTimesheet>($"{SelectFrom} WHERE `year` = @Year AND `month` = @Month ORDER BY `user_id`", new { Year = year, Month = month }, cancellationToken);

    public async Task SetCalculatedSalariesAsync(IReadOnlyDictionary<long, decimal> salaries, CancellationToken cancellationToken = default)
    {
        foreach (var (id, salary) in salaries)
        {
            await ExecuteAsync("UPDATE `monthly_timesheets` SET `calculated_salary` = @Salary WHERE `id` = @Id", new { Id = id, Salary = salary }, cancellationToken);
        }
    }

    public Task<int> FinalizeLockedAsync(int year, int month, CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE `monthly_timesheets` SET `status` = @Finalized WHERE `year` = @Year AND `month` = @Month AND `status` = @Locked",
            new { Year = year, Month = month, Finalized = TimesheetStatus.Finalized, Locked = TimesheetStatus.Locked }, cancellationToken);
}
