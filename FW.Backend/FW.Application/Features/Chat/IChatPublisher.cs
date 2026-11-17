namespace FW.Application.Features.Chat;

/// <summary>
/// Đẩy sự kiện chat tức thì tới các thiết bị đang online của người dùng (cài đặt bằng SignalR ở FW.API). Lỗi đẩy không được làm hỏng
/// việc gửi tin — tin nhắn đã lưu, người nhận vẫn thấy khi tải lại lịch sử.
/// </summary>
public interface IChatPublisher
{
    /// <summary>Tin nhắn mới tới mọi thành viên (kể cả các thiết bị khác của người gửi).</summary>
    Task MessageCreatedAsync(IReadOnlyCollection<int> userIds, ChatMessageDto message, CancellationToken cancellationToken = default);

    /// <summary>Tin nhắn được thu hồi.</summary>
    Task MessageDeletedAsync(IReadOnlyCollection<int> userIds, int conversationId, long messageId, CancellationToken cancellationToken = default);

    /// <summary>Người dùng đã đọc một phòng (đồng bộ số chưa đọc giữa các thiết bị của chính họ).</summary>
    Task ConversationReadAsync(int userId, int conversationId, int unreadCount, CancellationToken cancellationToken = default);
}
