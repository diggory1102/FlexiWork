using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

/// <summary>Cơ sở cho các kho lưu trữ của Trợ lý AI / cảnh báo nghỉ việc: dùng chung phiên và giao dịch (<see cref="DbSession"/>) như các repository khác.</summary>
public abstract class SessionStore
{
    static SessionStore() => DapperConfig.Configure();

    protected SessionStore(DbSession session) => Session = session;

    protected DbSession Session { get; }

    protected CommandDefinition Command(string sql, object? parameters, CancellationToken ct) =>
        new(sql, parameters, Session.Transaction, cancellationToken: ct);

    protected async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
    {
        var connection = await Session.GetConnectionAsync(ct);
        return (await connection.QueryAsync<T>(Command(sql, parameters, ct))).ToList();
    }

    protected async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct)
    {
        var connection = await Session.GetConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<T>(Command(sql, parameters, ct));
    }

    protected async Task<T> ExecuteScalarAsync<T>(string sql, object? parameters, CancellationToken ct)
    {
        var connection = await Session.GetConnectionAsync(ct);
        return (await connection.ExecuteScalarAsync<T>(Command(sql, parameters, ct)))!;
    }

    protected async Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct)
    {
        var connection = await Session.GetConnectionAsync(ct);
        return await connection.ExecuteAsync(Command(sql, parameters, ct));
    }
}

internal static class AiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string To<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T From<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

/// <summary>Hội thoại và tin nhắn của Trợ lý AI (migration 018).</summary>
public sealed class AssistantStore : SessionStore, IAssistantStore
{
    public AssistantStore(DbSession session) : base(session)
    {
    }

    private sealed class ConversationRow
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public AssistantConversation ToModel() => new(Id, UserId, Title, CreatedAt, UpdatedAt);
    }

    private sealed class MessageRow
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ToolTrace { get; set; }
        public string? DraftIds { get; set; }
        public DateTime CreatedAt { get; set; }

        public StoredMessage ToModel() => new(
            Id, ConversationId, Role, Content, ToolTrace,
            string.IsNullOrWhiteSpace(DraftIds)
                ? Array.Empty<Guid>()
                : DraftIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList(),
            CreatedAt);
    }

    private const string ConversationSelect =
        "SELECT `id` AS Id, `user_id` AS UserId, `title` AS Title, `created_at` AS CreatedAt, `updated_at` AS UpdatedAt FROM `assistant_conversations`";

    public async Task<AssistantConversation> CreateConversationAsync(int userId, string title, DateTime now, CancellationToken ct = default)
    {
        var safeTitle = title.Length > 200 ? title[..200] : title;
        var id = await ExecuteScalarAsync<long>(
            "INSERT INTO `assistant_conversations` (`user_id`, `title`, `created_at`, `updated_at`) VALUES (@userId, @title, @now, @now); SELECT LAST_INSERT_ID();",
            new { userId, title = safeTitle, now }, ct);
        return new AssistantConversation(id, userId, safeTitle, now, now);
    }

    public async Task<AssistantConversation?> GetConversationAsync(long conversationId, int userId, CancellationToken ct = default)
    {
        var row = await QuerySingleOrDefaultAsync<ConversationRow>(
            ConversationSelect + " WHERE `id` = @conversationId AND `user_id` = @userId", new { conversationId, userId }, ct);
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<AssistantConversation>> ListConversationsAsync(int userId, int limit, CancellationToken ct = default)
    {
        var rows = await QueryAsync<ConversationRow>(
            ConversationSelect + " WHERE `user_id` = @userId ORDER BY `updated_at` DESC, `id` DESC LIMIT @limit", new { userId, limit }, ct);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<StoredMessage> AddMessageAsync(
        long conversationId, string role, string content, string? toolTrace,
        IReadOnlyList<Guid> draftIds, DateTime now, CancellationToken ct = default)
    {
        var draftText = draftIds.Count == 0 ? null : string.Join(',', draftIds);
        var id = await Session.ExecuteInTransactionAsync(async token =>
        {
            var newId = await ExecuteScalarAsync<long>(
                """
                INSERT INTO `assistant_messages` (`conversation_id`, `role`, `content`, `tool_trace`, `draft_ids`, `created_at`)
                VALUES (@conversationId, @role, @content, @toolTrace, @draftText, @now);
                SELECT LAST_INSERT_ID();
                """, new { conversationId, role, content, toolTrace, draftText, now }, token);
            await ExecuteAsync("UPDATE `assistant_conversations` SET `updated_at` = @now WHERE `id` = @conversationId",
                new { conversationId, now }, token);
            return newId;
        }, ct);
        return new StoredMessage(id, conversationId, role, content, toolTrace, draftIds, now);
    }

    public async Task<IReadOnlyList<StoredMessage>> GetRecentMessagesAsync(long conversationId, int limit, CancellationToken ct = default)
    {
        var rows = await QueryAsync<MessageRow>(
            """
            SELECT `id` AS Id, `conversation_id` AS ConversationId, `role` AS Role, `content` AS Content,
                   `tool_trace` AS ToolTrace, `draft_ids` AS DraftIds, `created_at` AS CreatedAt
            FROM `assistant_messages` WHERE `conversation_id` = @conversationId ORDER BY `id` DESC LIMIT @limit
            """, new { conversationId, limit }, ct);
        return rows.Reverse().Select(r => r.ToModel()).ToList();
    }

    public Task<int> CountUserMessagesOnAsync(int userId, DateOnly date, CancellationToken ct = default)
    {
        var from = date.ToDateTime(TimeOnly.MinValue);
        return ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM `assistant_messages` m
            JOIN `assistant_conversations` c ON c.`id` = m.`conversation_id`
            WHERE c.`user_id` = @userId AND m.`role` = 'user' AND m.`created_at` >= @from AND m.`created_at` < @to
            """, new { userId, from, to = from.AddDays(1) }, ct);
    }
}

/// <summary>Bản nháp đơn do trợ lý soạn; đổi trạng thái nguyên tử để chống xác nhận hai lần.</summary>
public sealed class DraftStore : SessionStore, IDraftStore
{
    public DraftStore(DbSession session) : base(session)
    {
    }

    private sealed class DraftRow
    {
        // MySqlConnector trả cột CHAR(36) là Guid nên phải khai báo Guid, không phải string
        public Guid Id { get; set; }
        public long? ConversationId { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CardJson { get; set; } = string.Empty;
        public string SubmissionJson { get; set; } = string.Empty;
        public int? RequestId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    private static string Name(DraftStatus status) => status.ToString().ToLowerInvariant();

    public Task SaveAsync(AssistantDraft d, CancellationToken ct = default) =>
        ExecuteAsync(
            """
            INSERT INTO `assistant_drafts` (`id`, `conversation_id`, `user_id`, `status`, `card_json`, `submission_json`, `request_id`, `created_at`, `expires_at`)
            VALUES (@Id, @ConversationId, @UserId, @Status, @Card, @Submission, @RequestId, @CreatedAt, @ExpiresAt)
            ON DUPLICATE KEY UPDATE `status` = VALUES(`status`), `request_id` = VALUES(`request_id`)
            """,
            new
            {
                Id = d.Id.ToString(), d.ConversationId, d.UserId, Status = Name(d.Status),
                Card = AiJson.To(d.Card), Submission = AiJson.To(d.Submission), d.RequestId, d.CreatedAt, d.ExpiresAt
            }, ct);

    public async Task<AssistantDraft?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await QuerySingleOrDefaultAsync<DraftRow>(
            """
            SELECT `id` AS Id, `conversation_id` AS ConversationId, `user_id` AS UserId, `status` AS Status,
                   `card_json` AS CardJson, `submission_json` AS SubmissionJson, `request_id` AS RequestId,
                   `created_at` AS CreatedAt, `expires_at` AS ExpiresAt
            FROM `assistant_drafts` WHERE `id` = @id
            """, new { id = id.ToString() }, ct);
        if (r is null) return null;

        return new AssistantDraft
        {
            Id = r.Id,
            ConversationId = r.ConversationId,
            UserId = r.UserId,
            Card = AiJson.From<DraftCard>(r.CardJson),
            Submission = AiJson.From<LeaveRequestSubmission>(r.SubmissionJson),
            Status = Enum.Parse<DraftStatus>(r.Status, ignoreCase: true),
            RequestId = r.RequestId,
            CreatedAt = r.CreatedAt,
            ExpiresAt = r.ExpiresAt
        };
    }

    public async Task<bool> TryTransitionAsync(Guid id, DraftStatus from, DraftStatus to, CancellationToken ct = default)
    {
        var rows = await ExecuteAsync("UPDATE `assistant_drafts` SET `status` = @to WHERE `id` = @id AND `status` = @from",
            new { id = id.ToString(), from = Name(from), to = Name(to) }, ct);
        return rows == 1;
    }

    public Task SetRequestIdAsync(Guid id, int requestId, CancellationToken ct = default) =>
        ExecuteAsync("UPDATE `assistant_drafts` SET `request_id` = @requestId WHERE `id` = @id", new { id = id.ToString(), requestId }, ct);
}

/// <summary>Điểm nguy cơ nghỉ việc theo tuần (mỗi nhân viên một dòng mỗi tuần).</summary>
public sealed class AttritionSnapshotStore : SessionStore, IAttritionSnapshotStore
{
    public AttritionSnapshotStore(DbSession session) : base(session)
    {
    }

    private sealed class SnapshotRow
    {
        public int UserId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public DateTime WeekStart { get; set; }
        public int Score { get; set; }
        public string Level { get; set; } = "low";
        public bool DataSufficient { get; set; }
        public int? PreviousScore { get; set; }
        public string ReasonsJson { get; set; } = "[]";
        public string SignalsJson { get; set; } = "[]";
        public DateTime ComputedAt { get; set; }

        public AttritionSnapshot ToModel() => new(
            UserId, EmployeeName, BranchId, DateOnly.FromDateTime(WeekStart), Score,
            Enum.Parse<RiskLevel>(Level, ignoreCase: true), DataSufficient, PreviousScore,
            AiJson.From<List<string>>(ReasonsJson), AiJson.From<List<SignalResult>>(SignalsJson), ComputedAt);
    }

    private const string Select = """
        SELECT s.`user_id` AS UserId, u.`full_name` AS EmployeeName, u.`branch_id` AS BranchId, s.`week_start` AS WeekStart,
               s.`score` AS Score, s.`risk_level` AS Level, s.`data_sufficient` AS DataSufficient,
               s.`previous_score` AS PreviousScore, s.`reasons_json` AS ReasonsJson, s.`signals_json` AS SignalsJson,
               s.`computed_at` AS ComputedAt
        FROM `attrition_risk_snapshots` s
        JOIN `users` u ON u.`id` = s.`user_id`
        """;

    public Task UpsertAsync(IReadOnlyList<AttritionSnapshot> snapshots, CancellationToken ct = default)
    {
        if (snapshots.Count == 0) return Task.CompletedTask;
        return Session.ExecuteInTransactionAsync(async token =>
        {
            foreach (var s in snapshots)
            {
                await ExecuteAsync(
                    """
                    INSERT INTO `attrition_risk_snapshots`
                        (`user_id`, `week_start`, `score`, `risk_level`, `data_sufficient`, `previous_score`, `reasons_json`, `signals_json`, `computed_at`)
                    VALUES (@UserId, @WeekStart, @Score, @Level, @DataSufficient, @PreviousScore, @Reasons, @Signals, @ComputedAt)
                    ON DUPLICATE KEY UPDATE
                        `score` = VALUES(`score`), `risk_level` = VALUES(`risk_level`), `data_sufficient` = VALUES(`data_sufficient`),
                        `previous_score` = VALUES(`previous_score`), `reasons_json` = VALUES(`reasons_json`),
                        `signals_json` = VALUES(`signals_json`), `computed_at` = VALUES(`computed_at`)
                    """,
                    new
                    {
                        s.UserId, WeekStart = s.WeekStart.ToDateTime(TimeOnly.MinValue), s.Score,
                        Level = s.Level.ToString().ToLowerInvariant(), s.DataSufficient, s.PreviousScore,
                        Reasons = AiJson.To(s.Reasons), Signals = AiJson.To(s.Signals), s.ComputedAt
                    }, token);
            }
        }, ct);
    }

    public async Task<IReadOnlyList<AttritionSnapshot>> GetLatestAsync(int? branchId, CancellationToken ct = default)
    {
        var rows = await QueryAsync<SnapshotRow>(
            Select + """

            WHERE u.`is_deleted` = 0
              AND s.`week_start` = (SELECT MAX(x.`week_start`) FROM `attrition_risk_snapshots` x WHERE x.`user_id` = s.`user_id`)
              AND (@branchId IS NULL OR u.`branch_id` = @branchId)
            """, new { branchId }, ct);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<IReadOnlyList<AttritionSnapshot>> GetHistoryAsync(int userId, int weeks, CancellationToken ct = default)
    {
        var rows = await QueryAsync<SnapshotRow>(
            Select + " WHERE s.`user_id` = @userId ORDER BY s.`week_start` DESC LIMIT @weeks", new { userId, weeks }, ct);
        return rows.Reverse().Select(r => r.ToModel()).ToList();
    }
}
