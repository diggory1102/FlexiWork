using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Một thông báo gửi tới một người dùng (bảng notifications). Lưu lại để xem sau và đếm số chưa đọc;
/// khi người nhận đang online còn được đẩy tức thì qua NotificationHub (SignalR).
/// </summary>
public class Notification : BaseEntity<long>
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>Giá trị thuộc <see cref="Enums.NotificationType"/> — ứng dụng dựa vào đây để chọn biểu tượng và màn hình mở ra.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Mã thực thể liên quan để điều hướng (cách hiểu theo <see cref="Type"/>, xem <see cref="Enums.NotificationType"/>).</summary>
    public string? ReferenceId { get; set; }

    public bool IsRead { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
}
