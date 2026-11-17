using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;

namespace FW.Application.Features.Notifications;

public interface INotificationService
{
    /// <summary>Thông báo của tôi, mới nhất trước.</summary>
    Task<PaginatedResult<NotificationDto>> GetMineAsync(NotificationFilter filter, CancellationToken cancellationToken = default);

    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu đã đọc một thông báo của tôi (gọi lại không lỗi).</summary>
    Task MarkReadAsync(long id, CancellationToken cancellationToken = default);

    /// <returns>Số thông báo vừa được đánh dấu đã đọc.</returns>
    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Xem và đánh dấu đã đọc thông báo (UC 7.5). Mỗi người chỉ thấy thông báo của chính mình.</summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(INotificationRepository notifications, ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<PaginatedResult<NotificationDto>> GetMineAsync(NotificationFilter filter, CancellationToken cancellationToken = default) =>
        (await _notifications.GetPagedForUserAsync(CurrentUserId,
            new NotificationQuery { UnreadOnly = filter.UnreadOnly, PageIndex = filter.PageIndex, PageSize = filter.PageSize }, cancellationToken))
        .Map(NotificationDto.From);

    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
        new(await _notifications.CountUnreadAsync(CurrentUserId, cancellationToken));

    public async Task MarkReadAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!await _notifications.MarkReadAsync(id, CurrentUserId, cancellationToken)) throw new NotFoundException("Thông báo", id);
    }

    public Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default) =>
        _notifications.MarkAllReadAsync(CurrentUserId, cancellationToken);
}
