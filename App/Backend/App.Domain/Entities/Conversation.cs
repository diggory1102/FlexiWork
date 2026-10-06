using System.Collections.Generic;
namespace App.Domain.Entities
{
    public class Conversation : BaseEntity
    {
        public string? Name { get; set; }
        public bool IsGroup { get; set; }
        public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
