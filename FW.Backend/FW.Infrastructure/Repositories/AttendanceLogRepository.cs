using FW.Application.Common.Interfaces.Persistence;
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
