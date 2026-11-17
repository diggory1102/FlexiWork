using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Notifications;

public class Notifier : INotifier
{
    public const int MaxTitleLength = 150;
    public const int MaxContentLength = 500;
    public const int MaxReferenceLength = 50;

    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly INotificationPublisher _publisher;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<Notifier> _logger;

    public Notifier(INotificationRepository notifications, IUserRepository users, INotificationPublisher publisher, IDateTimeProvider clock,
        ILogger<Notifier> logger)
    {
        _notifications = notifications;
        _users = users;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    public async Task NotifyAsync(IEnumerable<int> userIds, string type, string title, string content, string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recipients = userIds.Distinct().ToArray();
            if (recipients.Length == 0) return;

            var rows = recipients.Select(id => new Notification
            {
                UserId = id, Type = type, Title = Truncate(title, MaxTitleLength), Content = Truncate(content, MaxContentLength),
                ReferenceId = referenceId is null ? null : Truncate(referenceId, MaxReferenceLength)
            }).ToList();
            await _notifications.AddRangeAsync(rows, cancellationToken);

            // Đẩy tức thì: lỗi mạng / hub không ảnh hưởng — thông báo đã nằm trong CSDL, người dùng vẫn thấy khi mở ứng dụng
            await _publisher.PublishAsync(recipients,
                new RealtimeNotification(rows[0].Title, rows[0].Content, type, rows[0].ReferenceId, _clock.Now), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gửi được thông báo {Type} tới {Count} người", type, userIds.Count());
        }
    }

    public async Task NotifyReviewersAsync(int? branchId, int requesterId, string type, string title, string content, string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var managers = branchId.HasValue
                ? await _users.GetActiveIdsByRoleAsync(RoleNames.Manager, branchId, cancellationToken)
                : Array.Empty<int>();
            var recipients = managers.Where(id => id != requesterId).ToList();
            if (managers.Contains(requesterId) || recipients.Count == 0)
            {
                recipients.AddRange((await _users.GetActiveIdsByRoleAsync(RoleNames.Hr, null, cancellationToken)).Where(id => id != requesterId));
            }
            await NotifyAsync(recipients, type, title, content, referenceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xác định được người duyệt của chi nhánh {BranchId} để gửi thông báo {Type}", branchId, type);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
