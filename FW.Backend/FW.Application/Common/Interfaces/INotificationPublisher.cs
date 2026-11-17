namespace FW.Application.Common.Interfaces;

/// <summary>Nội dung thông báo đẩy tức thì tới ứng dụng. Chỉ là tín hiệu — danh sách đầy đủ và số chưa đọc lấy qua API.</summary>
public record RealtimeNotification(string Title, string Content, string Type, string? ReferenceId, DateTime CreatedAt);

/// <summary>Đẩy thông báo tức thì tới các thiết bị đang online của người dùng (cài đặt bằng SignalR ở FW.API).</summary>
public interface INotificationPublisher
{
    Task PublishAsync(IReadOnlyCollection<int> userIds, RealtimeNotification notification, CancellationToken cancellationToken = default);
}
