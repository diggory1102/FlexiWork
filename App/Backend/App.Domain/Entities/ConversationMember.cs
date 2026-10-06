namespace App.Domain.Entities
{
    public class ConversationMember
    {
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
