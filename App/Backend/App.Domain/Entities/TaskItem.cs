using System;
using App.Domain.Enums;
namespace App.Domain.Entities
{
    public class TaskItem : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? AssigneeId { get; set; }
        public User? Assignee { get; set; }
        public int AssignerId { get; set; }
        public User Assigner { get; set; } = null!;
        public DateTime Deadline { get; set; }
        public Enums.TaskStatus Status { get; set; }
    }
}
