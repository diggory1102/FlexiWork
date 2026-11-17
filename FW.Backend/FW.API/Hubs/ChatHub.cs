using FW.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FW.API.Hubs;

/// <summary>
/// Kênh chat tức thì (/hubs/chat). Mỗi người dùng tham gia nhóm riêng <c>User_{id}</c> khi kết nối nên mọi thiết bị của họ đều nhận
/// tin nhắn. Gửi tin đi qua REST (<c>POST /api/chat/conversations/{id}/messages</c> — hỗ trợ cả ảnh) để mọi tin đều qua cùng một chỗ kiểm tra
/// quyền; máy chủ đẩy các sự kiện <see cref="ReceiveMessageMethod"/>, <see cref="MessageDeletedMethod"/>, <see cref="ConversationReadMethod"/>
/// xuống client.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    /// <summary>Có tin nhắn mới: <c>ChatMessageDto</c>.</summary>
    public const string ReceiveMessageMethod = "ReceiveMessage";

    /// <summary>Tin nhắn bị thu hồi: <c>{ conversationId, messageId }</c>.</summary>
    public const string MessageDeletedMethod = "MessageDeleted";

    /// <summary>Phòng đã được đọc ở một thiết bị khác của mình: <c>{ conversationId, unreadCount }</c>.</summary>
    public const string ConversationReadMethod = "ConversationRead";

    /// <summary>Nhóm chat thay đổi (thành viên, tên): <c>{ conversationId }</c> — client nạp lại danh sách phòng.</summary>
    public const string ConversationUpdatedMethod = "ConversationUpdated";

    public static string GroupFor(int userId) => $"User_{userId}";

    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirst(AppClaimTypes.UserId)?.Value, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
        }
        await base.OnConnectedAsync();
    }
}
