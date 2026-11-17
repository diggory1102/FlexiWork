using FW.API.Hubs;
using FW.Application.Features.Chat;
using Microsoft.AspNetCore.SignalR;

namespace FW.API.Services;

/// <summary>Đẩy sự kiện chat tới nhóm <c>User_{id}</c> của <see cref="ChatHub"/>.</summary>
public class SignalRChatPublisher : IChatPublisher
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalRChatPublisher(IHubContext<ChatHub> hub)
    {
        _hub = hub;
    }

    public Task MessageCreatedAsync(IReadOnlyCollection<int> userIds, ChatMessageDto message, CancellationToken cancellationToken = default) =>
        _hub.Clients.Groups(userIds.Select(ChatHub.GroupFor).ToList()).SendAsync(ChatHub.ReceiveMessageMethod, message, cancellationToken);

    public Task MessageDeletedAsync(IReadOnlyCollection<int> userIds, int conversationId, long messageId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Groups(userIds.Select(ChatHub.GroupFor).ToList())
            .SendAsync(ChatHub.MessageDeletedMethod, new { conversationId, messageId }, cancellationToken);

    public Task ConversationUpdatedAsync(IReadOnlyCollection<int> userIds, int conversationId, CancellationToken cancellationToken = default) =>
        _hub.Clients.Groups(userIds.Select(ChatHub.GroupFor).ToList()).SendAsync(ChatHub.ConversationUpdatedMethod, new { conversationId }, cancellationToken);

    public Task ConversationReadAsync(int userId, int conversationId, int unreadCount, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(ChatHub.GroupFor(userId)).SendAsync(ChatHub.ConversationReadMethod, new { conversationId, unreadCount }, cancellationToken);
}
