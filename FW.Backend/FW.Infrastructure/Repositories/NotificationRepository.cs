using System.Text;
using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class NotificationRepository : BaseRepository<Notification, long>, INotificationRepository
{
    private const int InsertBatchSize = 200;

    public NotificationRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "notifications";

    public async Task<PaginatedResult<Notification>> GetPagedForUserAsync(int userId, NotificationQuery query, CancellationToken cancellationToken = default)
    {
        var where = query.UnreadOnly ? "`user_id` = @UserId AND `is_read` = 0" : "`user_id` = @UserId";
        var sql = $"""
            SELECT COUNT(*) FROM `notifications` WHERE {where};
            {SelectFrom} WHERE {where} ORDER BY `created_at` DESC, `id` DESC LIMIT @Take OFFSET @Skip;
            """;
        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(
            Command(sql, new { UserId = userId, Take = query.NormalizedPageSize, Skip = query.Skip }, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<Notification>()).ToList();
        return PaginatedResult<Notification>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM `notifications` WHERE `user_id` = @UserId AND `is_read` = 0",
            new { UserId = userId }, cancellationToken);

    public async Task AddRangeAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default)
    {
        foreach (var batch in notifications.Chunk(InsertBatchSize))
        {
            var sql = new StringBuilder("INSERT INTO `notifications` (`user_id`, `title`, `content`, `type`, `reference_id`) VALUES ");
            var parameters = new DynamicParameters();
            for (var i = 0; i < batch.Length; i++)
            {
                var n = batch[i];
                if (i > 0) sql.Append(", ");
                sql.Append($"(@U{i}, @T{i}, @C{i}, @Ty{i}, @R{i})");
                parameters.Add($"U{i}", n.UserId);
                parameters.Add($"T{i}", n.Title);
                parameters.Add($"C{i}", n.Content);
                parameters.Add($"Ty{i}", n.Type);
                parameters.Add($"R{i}", n.ReferenceId);
            }
            await ExecuteAsync(sql.ToString(), parameters, cancellationToken);
        }
    }

    public async Task<bool> MarkReadAsync(long id, int userId, CancellationToken cancellationToken = default)
    {
        // Đã đọc từ trước vẫn tính là thành công (gọi lại không lỗi) — chỉ trả false khi không phải thông báo của người này
        var exists = await ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM `notifications` WHERE `id` = @Id AND `user_id` = @UserId)", new { Id = id, UserId = userId }, cancellationToken);
        if (!exists) return false;
        await ExecuteAsync("UPDATE `notifications` SET `is_read` = 1 WHERE `id` = @Id AND `user_id` = @UserId AND `is_read` = 0",
            new { Id = id, UserId = userId }, cancellationToken);
        return true;
    }

    public Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE `notifications` SET `is_read` = 1 WHERE `user_id` = @UserId AND `is_read` = 0", new { UserId = userId }, cancellationToken);
}
