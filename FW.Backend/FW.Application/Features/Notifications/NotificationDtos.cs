using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Features.Notifications;

public record NotificationDto(long Id, string Title, string Content, string Type, string? ReferenceId, bool IsRead, DateTime CreatedAt)
{
    public static NotificationDto From(Notification n) => new(n.Id, n.Title, n.Content, n.Type, n.ReferenceId, n.IsRead, n.CreatedAt);
}

public class NotificationFilter : PagedQuery
{
    /// <summary>true = chỉ thông báo chưa đọc.</summary>
    public bool UnreadOnly { get; set; }
}

public record UnreadCountDto(int Count);
