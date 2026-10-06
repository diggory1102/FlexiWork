namespace App.Domain.Entities
{
    public class Notification : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Type { get; set; } = string.Empty;
        public string? RefType { get; set; }
        public int? RefId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }
}
