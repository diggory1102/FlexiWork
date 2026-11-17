using FW.Application.Features.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Thông báo của tôi (UC 7.4, 7.5): danh sách, số chưa đọc, đánh dấu đã đọc. Thông báo mới còn được đẩy tức thì qua
/// SignalR tại /hubs/notifications (sự kiện "ReceiveNotification").
/// </summary>
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Thông báo của tôi, mới nhất trước; unreadOnly=true chỉ lấy chưa đọc.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] NotificationFilter filter, CancellationToken cancellationToken) =>
        Success(await _notificationService.GetMineAsync(filter, cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken) =>
        Success(await _notificationService.GetUnreadCountAsync(cancellationToken));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkReadAsync(id, cancellationToken);
        return Success("Đã đánh dấu đã đọc.");
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var count = await _notificationService.MarkAllReadAsync(cancellationToken);
        return Success(count, count > 0 ? $"Đã đánh dấu {count} thông báo là đã đọc." : "Không có thông báo nào chưa đọc.");
    }
}
