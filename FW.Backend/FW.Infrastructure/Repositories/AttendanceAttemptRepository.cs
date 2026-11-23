using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class AttendanceAttemptRepository : BaseRepository<AttendanceAttempt, long>, IAttendanceAttemptRepository
{
    public AttendanceAttemptRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "attendance_attempts";
    protected override string DefaultOrderBy => "`attempted_at` DESC, `id` DESC";

    static AttendanceAttemptRepository() => DapperConfig.Configure();

    public async Task<IReadOnlyList<AttendanceAttempt>> GetByPeriodAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE `attempted_at` >= @From AND `attempted_at` < @To ORDER BY `attempted_at`, `id`";
        return await QueryAsync<AttendanceAttempt>(sql, new { From = from, To = to }, cancellationToken);
    }
}
