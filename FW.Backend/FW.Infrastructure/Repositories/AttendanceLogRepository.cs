using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class AttendanceLogRepository : BaseRepository<AttendanceLog, long>, IAttendanceLogRepository
{
    public AttendanceLogRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "attendance_logs";
    protected override string DefaultOrderBy => "`checkin_time` DESC, `id` DESC";

    // l.* ánh xạ sang thuộc tính của AttendanceLog nhờ MatchNamesWithUnderscores (DapperConfig)
    private const string DetailSelect = """
        SELECT l.*,
               u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`avatar_url` AS AvatarUrl,
               b.`name` AS BranchName, b.`latitude` AS BranchLatitude, b.`longitude` AS BranchLongitude, b.`radius` AS BranchRadius,
               sh.`shift_code` AS ShiftCode, sh.`name` AS ShiftName, sh.`start_time` AS ShiftStartTime, sh.`end_time` AS ShiftEndTime,
               rv.`full_name` AS ReviewerName, adj.`full_name` AS CheckoutAdjusterName,
               EXISTS (SELECT 1 FROM `branch_wifis` w
                       WHERE w.`branch_id` = l.`branch_id` AND w.`is_deleted` = 0 AND w.`is_active` = 1) AS BranchRequiresWifi
        """;

    private const string DetailFrom = """
        FROM `attendance_logs` l
        JOIN `users` u ON u.`id` = l.`user_id`
        JOIN `branches` b ON b.`id` = l.`branch_id`
        LEFT JOIN `schedules` s ON s.`id` = l.`schedule_id`
        LEFT JOIN `shifts` sh ON sh.`id` = s.`shift_id`
        LEFT JOIN `users` rv ON rv.`id` = l.`reviewed_by`
        LEFT JOIN `users` adj ON adj.`id` = l.`checkout_adjusted_by`
        """;

    static AttendanceLogRepository() => DapperConfig.Configure();

    public async Task<PaginatedResult<AttendanceLogDetail>> GetDetailsPagedAsync(AttendanceLogQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "l.`work_date` BETWEEN @FromDate AND @ToDate" };
        var parameters = new DynamicParameters(new { FromDate = query.FromDate.Date, ToDate = query.ToDate.Date });

        if (query.BranchId.HasValue)
        {
            conditions.Add("l.`branch_id` = @BranchId");
            parameters.Add("BranchId", query.BranchId);
        }
        if (query.UserId.HasValue)
        {
            conditions.Add("l.`user_id` = @UserId");
            parameters.Add("UserId", query.UserId);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            conditions.Add("l.`status` = @Status");
            parameters.Add("Status", query.Status);
        }
        AddSearchCondition(query.Search, new[] { "u.`full_name`", "u.`employee_code`" }, conditions, parameters);

        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);
        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT COUNT(*) FROM `attendance_logs` l JOIN `users` u ON u.`id` = l.`user_id` WHERE {where};
            {DetailSelect} {DetailFrom}
            WHERE {where}
            ORDER BY l.`work_date` DESC, l.`checkin_time` DESC, l.`id` DESC
            LIMIT @Take OFFSET @Skip;
            """;

        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<AttendanceLogDetail>()).ToList();
        return PaginatedResult<AttendanceLogDetail>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    public Task<AttendanceLogDetail?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<AttendanceLogDetail>($"{DetailSelect} {DetailFrom} WHERE l.`id` = @Id", new { Id = id }, cancellationToken);

    public Task<AttendanceLog?> GetByScheduleIdAsync(long scheduleId, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<AttendanceLog>($"{SelectFrom} WHERE `schedule_id` = @ScheduleId", new { ScheduleId = scheduleId }, cancellationToken);

    public async Task<IReadOnlyList<AttendanceLog>> GetByScheduleIdsAsync(IEnumerable<long> scheduleIds, CancellationToken cancellationToken = default)
    {
        var ids = scheduleIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<AttendanceLog>();
        return await QueryAsync<AttendanceLog>($"{SelectFrom} WHERE `schedule_id` IN @Ids", new { Ids = ids }, cancellationToken);
    }

    public Task<AttendanceLog?> GetOpenByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<AttendanceLog>(
            $"{SelectFrom} WHERE `user_id` = @UserId AND `checkout_time` IS NULL ORDER BY `checkin_time` DESC, `id` DESC LIMIT 1",
            new { UserId = userId }, cancellationToken);

    public Task<IReadOnlyList<int>> GetOtherUsersOnDeviceAsync(string deviceId, DateTime workDate, int excludeUserId,
        CancellationToken cancellationToken = default) =>
        QueryAsync<int>(
            """
            SELECT DISTINCT `user_id` FROM `attendance_logs`
            WHERE `checkin_device_id` = @DeviceId AND `work_date` = @WorkDate AND `user_id` <> @UserId
            ORDER BY `user_id`
            """,
            new { DeviceId = deviceId, WorkDate = workDate.Date, UserId = excludeUserId }, cancellationToken);
}
