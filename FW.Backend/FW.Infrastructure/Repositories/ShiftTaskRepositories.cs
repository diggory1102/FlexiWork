using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class ShiftTaskRepository : BaseRepository<ShiftTask>, IShiftTaskRepository
{
    public ShiftTaskRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "shift_tasks";
    protected override string DefaultOrderBy => "`shift_id`, FIELD(`phase`, 'start', 'during', 'end'), `sort_order`, `id`";

    static ShiftTaskRepository() => DapperConfig.Configure();

    public Task<IReadOnlyList<ShiftTaskDetail>> GetTemplatesAsync(ShiftTaskQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "t.`is_deleted` = 0", "sh.`is_deleted` = 0" };
        var parameters = new DynamicParameters();
        if (query.ShiftId.HasValue)
        {
            conditions.Add("t.`shift_id` = @ShiftId");
            parameters.Add("ShiftId", query.ShiftId);
        }
        if (query.BranchId.HasValue)
        {
            conditions.Add("(t.`branch_id` IS NULL OR t.`branch_id` = @BranchId)");
            parameters.Add("BranchId", query.BranchId);
        }

        var sql = $"""
            SELECT t.*, sh.`shift_code` AS ShiftCode, sh.`name` AS ShiftName, b.`name` AS BranchName, p.`title` AS PositionTitle
            FROM `shift_tasks` t
            JOIN `shifts` sh ON sh.`id` = t.`shift_id`
            LEFT JOIN `branches` b ON b.`id` = t.`branch_id`
            LEFT JOIN `positions` p ON p.`id` = t.`position_id`
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY sh.`start_time`, sh.`shift_code`, FIELD(t.`phase`, 'start', 'during', 'end'), t.`sort_order`, t.`id`
            """;
        return QueryAsync<ShiftTaskDetail>(sql, parameters, cancellationToken);
    }

    public Task<IReadOnlyList<ShiftTask>> GetApplicableAsync(int shiftId, int branchId, int? positionId, CancellationToken cancellationToken = default) =>
        QueryAsync<ShiftTask>($"""
            {SelectFrom}
            WHERE `shift_id` = @ShiftId AND `is_deleted` = 0
              AND (`branch_id` IS NULL OR `branch_id` = @BranchId) AND (`position_id` IS NULL OR `position_id` = @PositionId)
            ORDER BY {DefaultOrderBy}
            """, new { ShiftId = shiftId, BranchId = branchId, PositionId = positionId }, cancellationToken);

    public Task<int> CountInScopeAsync(int shiftId, int? branchId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM `shift_tasks` WHERE `shift_id` = @ShiftId AND `branch_id` <=> @BranchId AND `is_deleted` = 0",
            new { ShiftId = shiftId, BranchId = branchId }, cancellationToken);
}

public class ShiftTaskAssignmentRepository : BaseRepository<ShiftTaskAssignment, long>, IShiftTaskAssignmentRepository
{
    public ShiftTaskAssignmentRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "shift_task_assignments";

    private const string DetailSelect = """
        SELECT a.*, t.`shift_id` AS ShiftId, t.`title` AS Title, t.`description` AS Description, t.`phase` AS Phase,
               t.`sort_order` AS SortOrder, t.`is_mandatory` AS IsMandatory, t.`requires_photo` AS RequiresPhoto,
               s.`user_id` AS ScheduleUserId, s.`branch_id` AS ScheduleBranchId, s.`status` AS ScheduleStatus
        FROM `shift_task_assignments` a
        JOIN `shift_tasks` t ON t.`id` = a.`task_id`
        JOIN `schedules` s ON s.`id` = a.`schedule_id`
        """;

    static ShiftTaskAssignmentRepository() => DapperConfig.Configure();

    public Task AssignForScheduleAsync(long scheduleId, int userId, int shiftId, int branchId, int? positionId,
        CancellationToken cancellationToken = default)
    {
        // Việc đã giao (UNIQUE schedule_id + task_id) giữ nguyên trạng thái
        const string sql = """
            INSERT INTO `shift_task_assignments` (`task_id`, `schedule_id`, `assigned_to`)
            SELECT t.`id`, @ScheduleId, @UserId FROM `shift_tasks` t
            WHERE t.`shift_id` = @ShiftId AND t.`is_deleted` = 0
              AND (t.`branch_id` IS NULL OR t.`branch_id` = @BranchId) AND (t.`position_id` IS NULL OR t.`position_id` = @PositionId)
            ON DUPLICATE KEY UPDATE `task_id` = `shift_task_assignments`.`task_id`
            """;
        return ExecuteAsync(sql, new { ScheduleId = scheduleId, UserId = userId, ShiftId = shiftId, BranchId = branchId, PositionId = positionId },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftTaskAssignmentDetail>> GetDetailsByScheduleIdsAsync(IEnumerable<long> scheduleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = scheduleIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<ShiftTaskAssignmentDetail>();
        return await QueryAsync<ShiftTaskAssignmentDetail>(
            $"{DetailSelect} WHERE a.`schedule_id` IN @Ids ORDER BY a.`schedule_id`, FIELD(t.`phase`, 'start', 'during', 'end'), t.`sort_order`, t.`id`",
            new { Ids = ids }, cancellationToken);
    }

    public Task<ShiftTaskAssignmentDetail?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<ShiftTaskAssignmentDetail>($"{DetailSelect} WHERE a.`id` = @Id", new { Id = id }, cancellationToken);

    public async Task<(int Total, int Pending)> CountAsync(long scheduleId, bool mandatoryOnly, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT COUNT(*) AS Total, COALESCE(SUM(a.`is_completed` = 0), 0) AS Pending
            FROM `shift_task_assignments` a JOIN `shift_tasks` t ON t.`id` = a.`task_id`
            WHERE a.`schedule_id` = @ScheduleId{(mandatoryOnly ? " AND t.`is_mandatory` = 1" : string.Empty)}
            """;
        var connection = await Session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<(long Total, decimal Pending)>(Command(sql, new { ScheduleId = scheduleId }, cancellationToken));
        return ((int)row.Total, (int)row.Pending);
    }

    public Task<IReadOnlyList<ShiftTaskProgressRow>> GetProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.`id` AS ScheduleId, s.`user_id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName,
                   u.`avatar_url` AS AvatarUrl, s.`branch_id` AS BranchId, b.`name` AS BranchName, s.`work_date` AS WorkDate,
                   sh.`name` AS ShiftName, sh.`start_time` AS StartTime, sh.`end_time` AS EndTime, s.`status` AS ScheduleStatus,
                   COUNT(a.`id`) AS Total,
                   CAST(COALESCE(SUM(a.`is_completed` = 1), 0) AS SIGNED) AS Done,
                   CAST(COALESCE(SUM(t.`is_mandatory` = 1), 0) AS SIGNED) AS MandatoryTotal,
                   CAST(COALESCE(SUM(t.`is_mandatory` = 1 AND a.`is_completed` = 1), 0) AS SIGNED) AS MandatoryDone
            FROM `schedules` s
            JOIN `users` u ON u.`id` = s.`user_id`
            JOIN `branches` b ON b.`id` = s.`branch_id`
            JOIN `shifts` sh ON sh.`id` = s.`shift_id`
            LEFT JOIN `shift_task_assignments` a ON a.`schedule_id` = s.`id`
            LEFT JOIN `shift_tasks` t ON t.`id` = a.`task_id`
            WHERE s.`work_date` = @WorkDate AND s.`is_published` = 1 AND s.`status` NOT IN @Excluded
              AND (@BranchId IS NULL OR s.`branch_id` = @BranchId)
            GROUP BY s.`id`, s.`user_id`, u.`employee_code`, u.`full_name`, u.`avatar_url`, s.`branch_id`, b.`name`, s.`work_date`,
                     sh.`name`, sh.`start_time`, sh.`end_time`, s.`status`
            ORDER BY b.`name`, sh.`start_time`, u.`full_name`
            """;
        return QueryAsync<ShiftTaskProgressRow>(sql, new
        {
            WorkDate = workDate.Date, BranchId = branchId, Excluded = new[] { ScheduleStatus.Cancelled, ScheduleStatus.OnLeave }
        }, cancellationToken);
    }
}
