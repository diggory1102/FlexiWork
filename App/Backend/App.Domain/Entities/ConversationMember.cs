using System;

namespace App.Domain.Entities
{
    /// <summary>
    /// Thành viên tham gia cuộc trò chuyện
    /// </summary>
    public class ConversationMember
    {
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
