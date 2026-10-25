namespace App.Domain.Entities
{
    /// <summary>
    /// Tin nhắn nội bộ (hỗ trợ SignalR)
    /// </summary>
    public class Message : BaseEntity<long>
    {
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        public int SenderId { get; set; }
        public User Sender { get; set; } = null!;

        public string? Content { get; set; }
        public string? ImageUrl { get; set; }
    }
}
