using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class ChatRepository : BaseRepository<Message, long>, IChatRepository
{
    public ChatRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "messages";

    static ChatRepository() => DapperConfig.Configure();

    // ---------------------------------------------------------------- Phòng chat

    public Task<Conversation?> GetConversationAsync(int id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<Conversation>(
            "SELECT `id` AS Id, `type` AS Type, `title` AS Title, `branch_id` AS BranchId, `created_by` AS CreatedBy, `unique_key` AS UniqueKey, `is_deleted` AS IsDeleted, `created_at` AS CreatedAt " +
            "FROM `conversations` WHERE `id` = @Id AND `is_deleted` = 0", new { Id = id }, cancellationToken);

    public async Task<int> GetOrCreateDirectAsync(int userA, int userB, CancellationToken cancellationToken = default)
    {
        // UNIQUE(unique_key) chặn tạo trùng khi hai người cùng bấm một lúc: INSERT IGNORE rồi đọc lại
        var key = Conversation.DirectKey(userA, userB);
        var id = await ExecuteScalarAsync<int>($"""
            INSERT IGNORE INTO `conversations` (`type`, `unique_key`) VALUES (@Type, @Key);
            SELECT `id` FROM `conversations` WHERE `unique_key` = @Key;
            """, new { Type = ConversationType.Direct, Key = key }, cancellationToken);
        await ExecuteAsync("INSERT IGNORE INTO `conversation_members` (`conversation_id`, `user_id`) VALUES (@Id, @A), (@Id, @B)",
            new { Id = id, A = userA, B = userB }, cancellationToken);
        return id;
    }

    public async Task<int> SyncBranchGroupAsync(int branchId, string title, CancellationToken cancellationToken = default)
    {
        var key = Conversation.BranchKey(branchId);
        var id = await ExecuteScalarAsync<int>("""
            INSERT IGNORE INTO `conversations` (`type`, `title`, `branch_id`, `unique_key`) VALUES (@Type, @Title, @BranchId, @Key);
            SELECT `id` FROM `conversations` WHERE `unique_key` = @Key;
            """, new { Type = ConversationType.Group, Title = title, BranchId = branchId, Key = key }, cancellationToken);

        await ExecuteAsync("UPDATE `conversations` SET `title` = @Title WHERE `id` = @Id AND (`title` IS NULL OR `title` <> @Title)",
            new { Id = id, Title = title }, cancellationToken);

        // Thêm nhân sự đang hoạt động của chi nhánh (đánh dấu đã đọc tới tin hiện có: người mới vào không bị "chưa đọc" cả lịch sử)
        await ExecuteAsync("""
            INSERT IGNORE INTO `conversation_members` (`conversation_id`, `user_id`, `last_read_message_id`)
            SELECT @Id, u.`id`, COALESCE((SELECT MAX(m.`id`) FROM `messages` m WHERE m.`conversation_id` = @Id), 0)
            FROM `users` u
            WHERE u.`branch_id` = @BranchId AND u.`is_deleted` = 0 AND u.`status` = @Active
              AND NOT EXISTS (SELECT 1 FROM `conversation_members` cm WHERE cm.`conversation_id` = @Id AND cm.`user_id` = u.`id`)
            """, new { Id = id, BranchId = branchId, Active = UserStatus.Active }, cancellationToken);

        // Bỏ người đã chuyển chi nhánh hoặc không còn hoạt động
        await ExecuteAsync("""
            DELETE cm FROM `conversation_members` cm JOIN `users` u ON u.`id` = cm.`user_id`
            WHERE cm.`conversation_id` = @Id
              AND (u.`branch_id` IS NULL OR u.`branch_id` <> @BranchId OR u.`is_deleted` = 1 OR u.`status` <> @Active)
            """, new { Id = id, BranchId = branchId, Active = UserStatus.Active }, cancellationToken);
        return id;
    }

    public Task LeaveOtherBranchGroupsAsync(int userId, int? currentBranchId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("""
            DELETE cm FROM `conversation_members` cm JOIN `conversations` c ON c.`id` = cm.`conversation_id`
            WHERE cm.`user_id` = @UserId AND c.`type` = @Group AND c.`branch_id` IS NOT NULL
              AND (@BranchId IS NULL OR c.`branch_id` <> @BranchId)
            """, new { UserId = userId, Group = ConversationType.Group, BranchId = currentBranchId }, cancellationToken);

    public Task<bool> IsMemberAsync(int conversationId, int userId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM `conversation_members` WHERE `conversation_id` = @C AND `user_id` = @U)",
            new { C = conversationId, U = userId }, cancellationToken);

    public Task<IReadOnlyList<int>> GetMemberIdsAsync(int conversationId, CancellationToken cancellationToken = default) =>
        QueryAsync<int>("SELECT `user_id` FROM `conversation_members` WHERE `conversation_id` = @C ORDER BY `user_id`", new { C = conversationId }, cancellationToken);

    public Task<IReadOnlyList<MemberRow>> GetMembersAsync(int conversationId, CancellationToken cancellationToken = default) =>
        QueryAsync<MemberRow>("""
            SELECT u.`id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`avatar_url` AS AvatarUrl, p.`title` AS PositionTitle
            FROM `conversation_members` cm JOIN `users` u ON u.`id` = cm.`user_id` LEFT JOIN `positions` p ON p.`id` = u.`position_id`
            WHERE cm.`conversation_id` = @C ORDER BY u.`full_name`
            """, new { C = conversationId }, cancellationToken);

    // ---------------------------------------------------------------- Danh sách phòng của tôi

    private const string RowSelect = """
        SELECT c.`id` AS Id, c.`type` AS Type, c.`title` AS Title, c.`branch_id` AS BranchId, c.`created_by` AS CreatedBy, c.`created_at` AS CreatedAt,
               (SELECT COUNT(*) FROM `conversation_members` x WHERE x.`conversation_id` = c.`id`) AS MemberCount,
               (SELECT COUNT(*) FROM `messages` m
                 WHERE m.`conversation_id` = c.`id` AND m.`id` > cm.`last_read_message_id` AND m.`sender_id` <> @Me AND m.`is_deleted` = 0) AS UnreadCount,
               ou.`id` AS OtherUserId, ou.`full_name` AS OtherFullName, ou.`avatar_url` AS OtherAvatarUrl,
               lm.`id` AS LastMessageId, lm.`sender_id` AS LastSenderId, lu.`full_name` AS LastSenderName, lm.`content` AS LastContent,
               (lm.`image_url` IS NOT NULL) AS LastHasImage, lm.`created_at` AS LastMessageAt
        FROM `conversation_members` cm
        JOIN `conversations` c ON c.`id` = cm.`conversation_id` AND c.`is_deleted` = 0
        LEFT JOIN `conversation_members` ocm ON ocm.`conversation_id` = c.`id` AND c.`type` = 'direct' AND ocm.`user_id` <> @Me
        LEFT JOIN `users` ou ON ou.`id` = ocm.`user_id`
        LEFT JOIN (SELECT m2.`conversation_id` AS ConversationId, MAX(m2.`id`) AS LastId FROM `messages` m2 WHERE m2.`is_deleted` = 0 GROUP BY m2.`conversation_id`) lx
               ON lx.ConversationId = c.`id`
        LEFT JOIN `messages` lm ON lm.`id` = lx.LastId
        LEFT JOIN `users` lu ON lu.`id` = lm.`sender_id`
        """;

    public Task<IReadOnlyList<ConversationRow>> GetConversationRowsAsync(int userId, CancellationToken cancellationToken = default) =>
        QueryAsync<ConversationRow>($"{RowSelect} WHERE cm.`user_id` = @Me ORDER BY COALESCE(lm.`created_at`, c.`created_at`) DESC, c.`id` DESC",
            new { Me = userId }, cancellationToken);

    public Task<ConversationRow?> GetConversationRowAsync(int conversationId, int userId, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<ConversationRow>($"{RowSelect} WHERE cm.`user_id` = @Me AND c.`id` = @C",
            new { Me = userId, C = conversationId }, cancellationToken);

    // ---------------------------------------------------------------- Tin nhắn

    private const string MessageSelect = """
        SELECT m.*, u.`full_name` AS SenderName, u.`avatar_url` AS SenderAvatarUrl
        FROM `messages` m JOIN `users` u ON u.`id` = m.`sender_id`
        """;

    public Task<MessageRow?> GetMessageAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<MessageRow>($"{MessageSelect} WHERE m.`id` = @Id", new { Id = id }, cancellationToken);

    public Task<IReadOnlyList<MessageRow>> GetMessagesAsync(int conversationId, long? beforeId, int take, CancellationToken cancellationToken = default) =>
        QueryAsync<MessageRow>($"{MessageSelect} WHERE m.`conversation_id` = @C AND (@Before IS NULL OR m.`id` < @Before) ORDER BY m.`id` DESC LIMIT @Take",
            new { C = conversationId, Before = beforeId, Take = take }, cancellationToken);

    public async Task<int> MarkReadAsync(int conversationId, int userId, long? upToMessageId, CancellationToken cancellationToken = default)
    {
        var latest = await ExecuteScalarAsync<long>("SELECT COALESCE(MAX(`id`), 0) FROM `messages` WHERE `conversation_id` = @C", new { C = conversationId }, cancellationToken);
        var target = Math.Min(upToMessageId ?? latest, latest); // không vượt quá tin hiện có
        await ExecuteAsync("""
            UPDATE `conversation_members` SET `last_read_message_id` = GREATEST(`last_read_message_id`, @Target)
            WHERE `conversation_id` = @C AND `user_id` = @U
            """, new { C = conversationId, U = userId, Target = target }, cancellationToken);
        return await ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM `messages` m JOIN `conversation_members` cm ON cm.`conversation_id` = m.`conversation_id` AND cm.`user_id` = @U
            WHERE m.`conversation_id` = @C AND m.`id` > cm.`last_read_message_id` AND m.`sender_id` <> @U AND m.`is_deleted` = 0
            """, new { C = conversationId, U = userId }, cancellationToken);
    }

    public async Task<bool> SoftDeleteMessageAsync(long messageId, int senderId, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("UPDATE `messages` SET `is_deleted` = 1 WHERE `id` = @Id AND `sender_id` = @S AND `is_deleted` = 0",
            new { Id = messageId, S = senderId }, cancellationToken) == 1;

    public Task<int> CountUnreadTotalAsync(int userId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM `messages` m
            JOIN `conversation_members` cm ON cm.`conversation_id` = m.`conversation_id` AND cm.`user_id` = @U
            JOIN `conversations` c ON c.`id` = m.`conversation_id` AND c.`is_deleted` = 0
            WHERE m.`id` > cm.`last_read_message_id` AND m.`sender_id` <> @U AND m.`is_deleted` = 0
            """, new { U = userId }, cancellationToken);

    // ---------------------------------------------------------------- Nhóm tùy chỉnh (UC 7.2)

    public async Task<int> CreateGroupAsync(string title, int createdBy, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken = default)
    {
        // Nhóm tùy chỉnh không có unique_key (được phép trùng tên) và không gắn chi nhánh
        var id = await ExecuteScalarAsync<int>("""
            INSERT INTO `conversations` (`type`, `title`, `created_by`) VALUES (@Type, @Title, @CreatedBy);
            SELECT LAST_INSERT_ID();
            """, new { Type = ConversationType.Group, Title = title, CreatedBy = createdBy }, cancellationToken);
        var everyone = memberIds.Append(createdBy).Distinct().Select(userId => new { Id = id, UserId = userId }).ToList();
        await ExecuteAsync("INSERT INTO `conversation_members` (`conversation_id`, `user_id`) VALUES (@Id, @UserId)", everyone, cancellationToken);
        return id;
    }

    public Task RenameGroupAsync(int conversationId, string title, CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE `conversations` SET `title` = @Title WHERE `id` = @Id AND `is_deleted` = 0", new { Id = conversationId, Title = title }, cancellationToken);

    public async Task<int> AddMembersAsync(int conversationId, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return 0;
        // Đánh dấu đã đọc tới tin hiện có để người mới vào không bị "chưa đọc" cả lịch sử
        return await ExecuteAsync("""
            INSERT IGNORE INTO `conversation_members` (`conversation_id`, `user_id`, `last_read_message_id`)
            SELECT @Id, u.`id`, COALESCE((SELECT MAX(m.`id`) FROM `messages` m WHERE m.`conversation_id` = @Id), 0)
            FROM `users` u WHERE u.`id` IN @UserIds
            """, new { Id = conversationId, UserIds = userIds.ToArray() }, cancellationToken);
    }

    public async Task<bool> RemoveMemberAsync(int conversationId, int userId, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("DELETE FROM `conversation_members` WHERE `conversation_id` = @C AND `user_id` = @U", new { C = conversationId, U = userId }, cancellationToken) == 1;

    public async Task<int?> GetOldestMemberAsync(int conversationId, int excludingUserId, CancellationToken cancellationToken = default) =>
        (await QueryAsync<int>("""
            SELECT `user_id` FROM `conversation_members` WHERE `conversation_id` = @C AND `user_id` <> @U ORDER BY `joined_at`, `user_id` LIMIT 1
            """, new { C = conversationId, U = excludingUserId }, cancellationToken)).Select(x => (int?)x).FirstOrDefault();

    public Task SetGroupOwnerAsync(int conversationId, int userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE `conversations` SET `created_by` = @U WHERE `id` = @C", new { C = conversationId, U = userId }, cancellationToken);

    public Task DeleteConversationAsync(int conversationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE `conversations` SET `is_deleted` = 1 WHERE `id` = @C", new { C = conversationId }, cancellationToken);
}
