using System.Text;
using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class ScheduleRepository : BaseRepository<Schedule, long>, IScheduleRepository
{
    /// <summary>Số dòng tối đa mỗi câu INSERT nhiều dòng (giữ số tham số dưới giới hạn của MySQL).</summary>
    private const int InsertBatchSize = 500;

    public ScheduleRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "schedules";
    protected override string DefaultOrderBy => "`work_date` ASC, `id` ASC";

    public Task<IReadOnlyList<ScheduleDetail>> GetDetailsAsync(ScheduleQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "s.`work_date` BETWEEN @FromDate AND @ToDate", "u.`is_deleted` = 0" };
        var parameters = new DynamicParameters(new { FromDate = query.FromDate.Date, ToDate = query.ToDate.Date });

        if (query.BranchId.HasValue)
        {
            conditions.Add("s.`branch_id` = @BranchId");
            parameters.Add("BranchId", query.BranchId);
        }
        if (query.UserId.HasValue)
        {
            conditions.Add("s.`user_id` = @UserId");
            parameters.Add("UserId", query.UserId);
        }
        if (query.PublishedOnly)
        {
            conditions.Add("s.`is_published` = 1");
        }

        var sql = $"""
            SELECT s.`id` AS Id, s.`user_id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName,
                   s.`shift_id` AS ShiftId, sh.`shift_code` AS ShiftCode, sh.`name` AS ShiftName,
                   sh.`start_time` AS StartTime, sh.`end_time` AS EndTime, sh.`is_cross_day` AS IsCrossDay,
                   s.`branch_id` AS BranchId, s.`work_date` AS WorkDate, s.`status` AS Status,
                   s.`is_published` AS IsPublished, s.`published_at` AS PublishedAt
            FROM `schedules` s
            JOIN `users` u ON u.`id` = s.`user_id`
            JOIN `shifts` sh ON sh.`id` = s.`shift_id`
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY s.`work_date`, sh.`start_time`, u.`full_name`
            """;
        return QueryAsync<ScheduleDetail>(sql, parameters, cancellationToken);
    }

    public Task<IReadOnlyList<Schedule>> GetActiveByUserAsync(int userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            {SelectFrom}
            WHERE `user_id` = @UserId AND `work_date` BETWEEN @FromDate AND @ToDate AND `status` <> @Cancelled
            ORDER BY {DefaultOrderBy}
            """;
        return QueryAsync<Schedule>(sql,
            new { UserId = userId, FromDate = fromDate.Date, ToDate = toDate.Date, Cancelled = ScheduleStatus.Cancelled },
            cancellationToken);
    }

    public Task<bool> ExistsAsync(int userId, int shiftId, DateTime workDate, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM `schedules` WHERE `user_id` = @UserId AND `shift_id` = @ShiftId AND `work_date` = @WorkDate)",
            new { UserId = userId, ShiftId = shiftId, WorkDate = workDate.Date }, cancellationToken);

    public async Task<int> AddRangeAsync(IReadOnlyCollection<Schedule> schedules, CancellationToken cancellationToken = default)
    {
        var inserted = 0;
        foreach (var batch in schedules.Chunk(InsertBatchSize))
        {
            var sql = new StringBuilder(
                "INSERT INTO `schedules` (`user_id`, `shift_id`, `branch_id`, `work_date`, `status`, `is_published`, `published_at`, `created_by`) VALUES ");
            var parameters = new DynamicParameters();
            for (var i = 0; i < batch.Length; i++)
            {
                var s = batch[i];
                if (i > 0) sql.Append(", ");
                sql.Append($"(@U{i}, @Sh{i}, @B{i}, @D{i}, @St{i}, @P{i}, @Pa{i}, @C{i})");
                parameters.Add($"U{i}", s.UserId);
                parameters.Add($"Sh{i}", s.ShiftId);
                parameters.Add($"B{i}", s.BranchId);
                parameters.Add($"D{i}", s.WorkDate.Date);
                parameters.Add($"St{i}", s.Status);
                parameters.Add($"P{i}", s.IsPublished);
                parameters.Add($"Pa{i}", s.PublishedAt);
                parameters.Add($"C{i}", s.CreatedBy);
            }
            inserted += await ExecuteAsync(sql.ToString(), parameters, cancellationToken);
        }
        return inserted;
    }

    public Task<int> PublishAsync(int branchId, DateTime fromDate, DateTime toDate, DateTime publishedAt, CancellationToken cancellationToken = default) =>
        ExecuteAsync("""
            UPDATE `schedules` SET `is_published` = 1, `published_at` = @PublishedAt
            WHERE `branch_id` = @BranchId AND `work_date` BETWEEN @FromDate AND @ToDate AND `is_published` = 0
            """,
            new { BranchId = branchId, FromDate = fromDate.Date, ToDate = toDate.Date, PublishedAt = publishedAt },
            cancellationToken);
}
