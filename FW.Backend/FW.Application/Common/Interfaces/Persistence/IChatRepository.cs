using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>Một dòng danh sách phòng chat của một người: phòng + tin nhắn mới nhất + số chưa đọc (đọc bằng một truy vấn).</summary>
public class ConversationRow
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int? BranchId { get; set; }

    /// <summary>Người tạo nhóm tùy chỉnh (quản trị nhóm); null với chat 1-1 và nhóm chi nhánh.</summary>
    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public int UnreadCount { get; set; }

    /// <summary>Chat trực tiếp: người còn lại.</summary>
    public int? OtherUserId { get; set; }
    public string? OtherFullName { get; set; }
    public string? OtherAvatarUrl { get; set; }

    public long? LastMessageId { get; set; }
    public int? LastSenderId { get; set; }
    public string? LastSenderName { get; set; }
    public string? LastContent { get; set; }
    public bool LastHasImage { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>Tin nhắn kèm người gửi.</summary>
public class MessageRow : Message
{
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
}

/// <summary>Thành viên phòng chat.</summary>
public class MemberRow
{
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PositionTitle { get; set; }
}

public interface IChatRepository : IGenericRepository<Message, long>
{
    Task<Conversation?> GetConversationAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Phòng chat trực tiếp của hai người — tạo nếu chưa có (an toàn khi hai người cùng bấm một lúc).</summary>
    Task<int> GetOrCreateDirectAsync(int userA, int userB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhóm chat của chi nhánh (tạo nếu chưa có, cập nhật tên theo chi nhánh) và đồng bộ thành viên: thêm mọi nhân sự đang hoạt động
    /// của chi nhánh, bỏ người đã chuyển chi nhánh hoặc nghỉ việc. Thành viên mới không thấy lịch sử cũ là "chưa đọc".
    /// </summary>
    Task<int> SyncBranchGroupAsync(int branchId, string title, CancellationToken cancellationToken = default);

    /// <summary>Bỏ người dùng khỏi các nhóm chi nhánh khác chi nhánh hiện tại của họ.</summary>
    Task LeaveOtherBranchGroupsAsync(int userId, int? currentBranchId, CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(int conversationId, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetMemberIdsAsync(int conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberRow>> GetMembersAsync(int conversationId, CancellationToken cancellationToken = default);

    /// <summary>Phòng chat của người dùng, hoạt động gần nhất trước.</summary>
    Task<IReadOnlyList<ConversationRow>> GetConversationRowsAsync(int userId, CancellationToken cancellationToken = default);

    Task<ConversationRow?> GetConversationRowAsync(int conversationId, int userId, CancellationToken cancellationToken = default);

    Task<MessageRow?> GetMessageAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử: tối đa <paramref name="take"/> tin có id nhỏ hơn <paramref name="beforeId"/> (bỏ qua nếu null), mới nhất trước.</summary>
    Task<IReadOnlyList<MessageRow>> GetMessagesAsync(int conversationId, long? beforeId, int take, CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu đã đọc tới tin <paramref name="upToMessageId"/> (null = tin mới nhất); không bao giờ lùi lại.</summary>
    /// <returns>Số tin chưa đọc còn lại sau khi đánh dấu.</returns>
    Task<int> MarkReadAsync(int conversationId, int userId, long? upToMessageId, CancellationToken cancellationToken = default);

    /// <summary>Thu hồi tin nhắn của chính người gửi (xóa mềm).</summary>
    Task<bool> SoftDeleteMessageAsync(long messageId, int senderId, CancellationToken cancellationToken = default);

    Task<int> CountUnreadTotalAsync(int userId, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Nhóm tùy chỉnh (UC 7.2)

    /// <summary>Tạo nhóm tùy chỉnh do <paramref name="createdBy"/> quản trị; thành viên gồm người tạo và <paramref name="memberIds"/>. Gọi trong giao dịch.</summary>
    Task<int> CreateGroupAsync(string title, int createdBy, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken = default);

    Task RenameGroupAsync(int conversationId, string title, CancellationToken cancellationToken = default);

    /// <summary>Thêm thành viên (bỏ qua người đã có); người mới không thấy lịch sử cũ là "chưa đọc". Trả về số người thật sự được thêm.</summary>
    Task<int> AddMembersAsync(int conversationId, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    Task<bool> RemoveMemberAsync(int conversationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Thành viên vào nhóm sớm nhất (loại trừ một người) — người kế nhiệm khi quản trị nhóm rời đi; null nếu không còn ai.</summary>
    Task<int?> GetOldestMemberAsync(int conversationId, int excludingUserId, CancellationToken cancellationToken = default);

    Task SetGroupOwnerAsync(int conversationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Xóa mềm phòng chat (nhóm không còn thành viên).</summary>
    Task DeleteConversationAsync(int conversationId, CancellationToken cancellationToken = default);
}
