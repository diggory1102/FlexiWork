using System;
using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Cuộc trò chuyện trực tiếp (1-1) hoặc nhóm chat chi nhánh
    /// </summary>
    public class Conversation : BaseEntity
    {
        public string Type { get; set; } = "direct"; // direct, group
        public string? Title { get; set; }
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        // Navigation Properties
        public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
