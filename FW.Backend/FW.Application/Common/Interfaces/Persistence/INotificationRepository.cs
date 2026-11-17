using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public class NotificationQuery : PagedQuery
{
    /// <summary>true = chỉ thông báo chưa đọc.</summary>
    public bool UnreadOnly { get; set; }
}

public interface INotificationRepository : IGenericRepository<Notification, long>
{
    /// <summary>Thông báo của một người, mới nhất trước.</summary>
    Task<PaginatedResult<Notification>> GetPagedForUserAsync(int userId, NotificationQuery query, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Thêm nhiều thông báo bằng câu INSERT nhiều dòng (gửi cho cả chi nhánh vẫn chỉ vài truy vấn).</summary>
    Task AddRangeAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu đã đọc một thông báo của chính người đó.</summary>
    /// <returns>false nếu thông báo không tồn tại hoặc không phải của người này.</returns>
    Task<bool> MarkReadAsync(long id, int userId, CancellationToken cancellationToken = default);

    /// <returns>Số thông báo vừa được đánh dấu.</returns>
    Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}
