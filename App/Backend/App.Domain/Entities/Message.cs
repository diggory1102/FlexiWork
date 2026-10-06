namespace App.Domain.Entities
{
    public class Message : BaseEntity
    {
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;
        public int? SenderId { get; set; }
        public User? Sender { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
