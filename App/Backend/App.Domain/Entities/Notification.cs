namespace App.Domain.Entities
{
    /// <summary>
    /// Thông báo hệ thống gửi đến người dùng
    /// </summary>
    public class Notification : BaseEntity<long>
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public bool IsRead { get; set; } = false;
    }
}
