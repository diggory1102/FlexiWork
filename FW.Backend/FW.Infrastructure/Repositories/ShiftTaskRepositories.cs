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
               t.`sort_order` AS SortOrder, t.`is_mandatory` AS IsMandatory, t.`requires_photo` AS RequiresPhoto, t.`assign_mode` AS AssignMode,
               s.`user_id` AS ScheduleUserId, s.`branch_id` AS ScheduleBranchId, s.`work_date` AS ScheduleWorkDate, s.`status` AS ScheduleStatus,
               cu.`full_name` AS CompletedByName
        FROM `shift_task_assignments` a
        JOIN `shift_tasks` t ON t.`id` = a.`task_id`
        JOIN `schedules` s ON s.`id` = a.`schedule_id`
        LEFT JOIN `users` cu ON cu.`id` = a.`completed_by`
        """;

    /// <summary>
    /// Việc làm chung: bản việc của người đến sau (check-in muộn, hoặc việc mới đổi sang làm chung) mang luôn kết quả của người đã làm,
    /// để cả ca cùng thấy "đã xong". Chỉ xét các ca vài ngày gần đây (ca đang diễn ra luôn nằm trong khoảng này).
    /// </summary>
    private const string SyncSharedSql = """
        UPDATE `shift_task_assignments` a
        JOIN `shift_tasks` t ON t.`id` = a.`task_id` AND t.`assign_mode` = 'shared'
        JOIN `schedules` s ON s.`id` = a.`schedule_id`
        JOIN (SELECT a2.`task_id` AS task_id, s2.`branch_id` AS branch_id, s2.`work_date` AS work_date, MIN(a2.`id`) AS src_id
              FROM `shift_task_assignments` a2 JOIN `schedules` s2 ON s2.`id` = a2.`schedule_id`
              WHERE a2.`is_completed` = 1 AND s2.`work_date` >= DATE_SUB(CURDATE(), INTERVAL 3 DAY)
              GROUP BY a2.`task_id`, s2.`branch_id`, s2.`work_date`) d
             ON d.`task_id` = a.`task_id` AND d.`branch_id` = s.`branch_id` AND d.`work_date` = s.`work_date`
        JOIN `shift_task_assignments` src ON src.`id` = d.`src_id`
        SET a.`is_completed` = 1, a.`completed_at` = src.`completed_at`, a.`completed_by` = COALESCE(src.`completed_by`, src.`assigned_to`),
            a.`proof_image_url` = src.`proof_image_url`, a.`note` = src.`note`
        WHERE a.`is_completed` = 0 AND
        """;

    private sealed class GroupRow
    {
        public long Id { get; set; }
        public long Done { get; set; }
        public string? ProofImagePublicId { get; set; }
    }

    private const string GroupMembers =
        "a.`task_id` = @TaskId AND a.`schedule_id` IN (SELECT `id` FROM `schedules` WHERE `branch_id` = @BranchId AND `work_date` = @WorkDate)";

    static ShiftTaskAssignmentRepository() => DapperConfig.Configure();

    public async Task AssignForScheduleAsync(long scheduleId, int userId, int shiftId, int branchId, int? positionId,
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
        await ExecuteAsync(sql, new { ScheduleId = scheduleId, UserId = userId, ShiftId = shiftId, BranchId = branchId, PositionId = positionId },
            cancellationToken);
        await ExecuteAsync(SyncSharedSql + " a.`schedule_id` = @ScheduleId", new { ScheduleId = scheduleId }, cancellationToken);
    }

    public async Task<int> AssignTemplateToInProgressAsync(int taskId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO `shift_task_assignments` (`task_id`, `schedule_id`, `assigned_to`)
            SELECT t.`id`, s.`id`, s.`user_id`
            FROM `shift_tasks` t
            JOIN `schedules` s ON s.`shift_id` = t.`shift_id` AND s.`status` = @InProgress AND (t.`branch_id` IS NULL OR t.`branch_id` = s.`branch_id`)
            JOIN `users` u ON u.`id` = s.`user_id`
            WHERE t.`id` = @TaskId AND t.`is_deleted` = 0 AND (t.`position_id` IS NULL OR t.`position_id` = u.`position_id`)
            ON DUPLICATE KEY UPDATE `task_id` = `shift_task_assignments`.`task_id`
            """;
        var added = await ExecuteAsync(sql, new { TaskId = taskId, InProgress = ScheduleStatus.InProgress }, cancellationToken);
        await ExecuteAsync(SyncSharedSql + " a.`task_id` = @TaskId", new { TaskId = taskId }, cancellationToken); // đổi sang làm chung: nhóm cùng thấy kết quả
        return added;
    }

    public async Task<int> AssignForInProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO `shift_task_assignments` (`task_id`, `schedule_id`, `assigned_to`)
            SELECT t.`id`, s.`id`, s.`user_id`
            FROM `schedules` s
            JOIN `users` u ON u.`id` = s.`user_id`
            JOIN `shift_tasks` t ON t.`shift_id` = s.`shift_id` AND t.`is_deleted` = 0
                 AND (t.`branch_id` IS NULL OR t.`branch_id` = s.`branch_id`) AND (t.`position_id` IS NULL OR t.`position_id` = u.`position_id`)
            WHERE s.`work_date` = @WorkDate AND s.`status` = @InProgress AND (@BranchId IS NULL OR s.`branch_id` = @BranchId)
            ON DUPLICATE KEY UPDATE `task_id` = `shift_task_assignments`.`task_id`
            """;
        var added = await ExecuteAsync(sql, new { WorkDate = workDate.Date, BranchId = branchId, InProgress = ScheduleStatus.InProgress }, cancellationToken);
        await ExecuteAsync(SyncSharedSql + " s.`work_date` = @WorkDate AND (@BranchId IS NULL OR s.`branch_id` = @BranchId)",
            new { WorkDate = workDate.Date, BranchId = branchId }, cancellationToken);
        return added;
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
                   CAST(COALESCE(SUM(t.`is_mandatory` = 1 AND a.`is_completed` = 1), 0) AS SIGNED) AS MandatoryDone,
                   (SELECT COUNT(*) FROM `shift_tasks` x WHERE x.`shift_id` = s.`shift_id` AND x.`is_deleted` = 0
                       AND (x.`branch_id` IS NULL OR x.`branch_id` = s.`branch_id`) AND (x.`position_id` IS NULL OR x.`position_id` = u.`position_id`)) AS Expected,
                   (SELECT COUNT(*) FROM `shift_tasks` x WHERE x.`shift_id` = s.`shift_id` AND x.`is_deleted` = 0 AND x.`is_mandatory` = 1
                       AND (x.`branch_id` IS NULL OR x.`branch_id` = s.`branch_id`) AND (x.`position_id` IS NULL OR x.`position_id` = u.`position_id`)) AS ExpectedMandatory
            FROM `schedules` s
            JOIN `users` u ON u.`id` = s.`user_id`
            JOIN `branches` b ON b.`id` = s.`branch_id`
            JOIN `shifts` sh ON sh.`id` = s.`shift_id`
            LEFT JOIN `shift_task_assignments` a ON a.`schedule_id` = s.`id`
            LEFT JOIN `shift_tasks` t ON t.`id` = a.`task_id`
            WHERE s.`work_date` = @WorkDate AND s.`is_published` = 1 AND s.`status` NOT IN @Excluded
              AND (@BranchId IS NULL OR s.`branch_id` = @BranchId)
            GROUP BY s.`id`, s.`user_id`, u.`employee_code`, u.`full_name`, u.`avatar_url`, u.`position_id`, s.`shift_id`, s.`branch_id`, b.`name`, s.`work_date`,
                     sh.`name`, sh.`start_time`, sh.`end_time`, s.`status`
            ORDER BY b.`name`, sh.`start_time`, u.`full_name`
            """;
        return QueryAsync<ShiftTaskProgressRow>(sql, new
        {
            WorkDate = workDate.Date, BranchId = branchId, Excluded = new[] { ScheduleStatus.Cancelled, ScheduleStatus.OnLeave }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SharedTaskProgressRow>> GetSharedProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT t.`id` AS TaskId, t.`title` AS Title, t.`phase` AS Phase, t.`is_mandatory` AS IsMandatory,
                   s.`branch_id` AS BranchId, b.`name` AS BranchName, sh.`name` AS ShiftName, sh.`start_time` AS StartTime, sh.`end_time` AS EndTime,
                   COUNT(a.`id`) AS Participants,
                   COALESCE(MAX(a.`is_completed`), 0) AS IsDone,
                   MAX(CASE WHEN a.`is_completed` = 1 THEN COALESCE(cu.`full_name`, au.`full_name`) END) AS CompletedByName,
                   MAX(a.`completed_at`) AS CompletedAt
            FROM `shift_task_assignments` a
            JOIN `shift_tasks` t ON t.`id` = a.`task_id` AND t.`assign_mode` = 'shared' AND t.`is_deleted` = 0
            JOIN `schedules` s ON s.`id` = a.`schedule_id`
            JOIN `branches` b ON b.`id` = s.`branch_id`
            JOIN `shifts` sh ON sh.`id` = s.`shift_id`
            LEFT JOIN `users` cu ON cu.`id` = a.`completed_by`
            LEFT JOIN `users` au ON au.`id` = a.`assigned_to`
            WHERE s.`work_date` = @WorkDate AND s.`is_published` = 1 AND s.`status` NOT IN @Excluded
              AND (@BranchId IS NULL OR s.`branch_id` = @BranchId)
            GROUP BY t.`id`, t.`title`, t.`phase`, t.`is_mandatory`, t.`sort_order`, s.`branch_id`, b.`name`, sh.`name`, sh.`start_time`, sh.`end_time`
            ORDER BY b.`name`, sh.`start_time`, FIELD(t.`phase`, 'start', 'during', 'end'), t.`sort_order`, t.`id`
            """;
        return QueryAsync<SharedTaskProgressRow>(sql, new
        {
            WorkDate = workDate.Date, BranchId = branchId, Excluded = new[] { ScheduleStatus.Cancelled, ScheduleStatus.OnLeave }
        }, cancellationToken);
    }

    public Task<bool> TryCompleteAsync(ShiftTaskAssignmentDetail a, DateTime completedAt, string? proofImageUrl, string? proofImagePublicId,
        string? note, CancellationToken cancellationToken = default) =>
        Session.ExecuteInTransactionAsync<bool>(async ct =>
        {
            var shared = a.AssignMode == FW.Domain.Enums.TaskAssignMode.Shared;
            var parameters = new
            {
                a.Id, a.TaskId, BranchId = a.ScheduleBranchId, WorkDate = a.ScheduleWorkDate.Date, UserId = a.AssignedTo,
                At = completedAt, Url = proofImageUrl, PublicId = proofImagePublicId, Note = note
            };

            // Khóa (FOR UPDATE: đọc bản mới nhất đã commit) mọi bản việc cùng nhóm rồi mới kiểm tra — hai người bấm cùng lúc thì người sau
            // chờ người trước xong, thấy "đã có người làm" và thua
            var group = await QueryAsync<GroupRow>(shared
                ? $"SELECT a.`id` AS Id, CAST(a.`is_completed` AS SIGNED) AS Done FROM `shift_task_assignments` a WHERE {GroupMembers} FOR UPDATE"
                : "SELECT `id` AS Id, CAST(`is_completed` AS SIGNED) AS Done FROM `shift_task_assignments` WHERE `id` = @Id FOR UPDATE",
                parameters, ct);
            if (group.Count == 0 || group.Any(g => g.Done == 1)) return false;

            await ExecuteAsync("""
                UPDATE `shift_task_assignments`
                SET `is_completed` = 1, `completed_at` = @At, `completed_by` = @UserId,
                    `proof_image_url` = @Url, `proof_image_public_id` = @PublicId, `note` = @Note
                WHERE `id` = @Id
                """, parameters, ct);
            if (shared)
            {
                await ExecuteAsync($"""
                    UPDATE `shift_task_assignments` a
                    SET a.`is_completed` = 1, a.`completed_at` = @At, a.`completed_by` = @UserId, a.`proof_image_url` = @Url, a.`note` = @Note
                    WHERE {GroupMembers} AND a.`id` <> @Id AND a.`is_completed` = 0
                    """, parameters, ct);
            }
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<string>> UncompleteAsync(ShiftTaskAssignmentDetail a, CancellationToken cancellationToken = default) =>
        Session.ExecuteInTransactionAsync<IReadOnlyList<string>>(async ct =>
        {
            var shared = a.AssignMode == FW.Domain.Enums.TaskAssignMode.Shared;
            var parameters = new
            {
                a.Id, a.TaskId, BranchId = a.ScheduleBranchId, WorkDate = a.ScheduleWorkDate.Date, Completer = a.EffectiveCompletedBy ?? a.AssignedTo
            };

            // Việc làm chung: chỉ bỏ những bản đã xong bởi cùng người hoàn thành (không đụng bản xong do người khác, nếu có)
            var rows = await QueryAsync<GroupRow>(shared
                ? $"""
                    SELECT a.`id` AS Id, 1 AS Done, a.`proof_image_public_id` AS ProofImagePublicId FROM `shift_task_assignments` a
                    WHERE {GroupMembers} AND a.`is_completed` = 1 AND COALESCE(a.`completed_by`, a.`assigned_to`) = @Completer FOR UPDATE
                    """
                : "SELECT `id` AS Id, 1 AS Done, `proof_image_public_id` AS ProofImagePublicId FROM `shift_task_assignments` WHERE `id` = @Id AND `is_completed` = 1 FOR UPDATE",
                parameters, ct);
            if (rows.Count == 0) return Array.Empty<string>();

            await ExecuteAsync("""
                UPDATE `shift_task_assignments`
                SET `is_completed` = 0, `completed_at` = NULL, `completed_by` = NULL, `proof_image_url` = NULL, `proof_image_public_id` = NULL, `note` = NULL
                WHERE `id` IN @Ids
                """, new { Ids = rows.Select(r => r.Id).ToArray() }, ct);
            return rows.Where(r => r.ProofImagePublicId is not null).Select(r => r.ProofImagePublicId!).ToList();
        }, cancellationToken);
}
