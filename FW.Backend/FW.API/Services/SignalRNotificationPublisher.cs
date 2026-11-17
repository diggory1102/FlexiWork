using FW.API.Hubs;
using FW.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FW.API.Services;

/// <summary>Đẩy thông báo tới nhóm <c>User_{id}</c> của <see cref="NotificationHub"/>.</summary>
public class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task PublishAsync(IReadOnlyCollection<int> userIds, RealtimeNotification notification, CancellationToken cancellationToken = default) =>
        _hub.Clients.Groups(userIds.Select(NotificationHub.GroupFor).ToList())
            .SendAsync(NotificationHub.ReceiveNotificationMethod, notification, cancellationToken);
}
