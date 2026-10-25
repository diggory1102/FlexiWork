using System;

namespace App.Domain.Entities
{
    /// <summary>
    /// Đầu việc checklist thực tế phân công trong ca trực
    /// </summary>
    public class ShiftTaskAssignment : BaseEntity<long>
    {
        public int TaskId { get; set; }
        public ShiftTask Task { get; set; } = null!;

        public long ScheduleId { get; set; }
        public Schedule Schedule { get; set; } = null!;

        public int AssignedTo { get; set; }
        public User Assignee { get; set; } = null!;

        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
        public string? ProofImageUrl { get; set; }
        public string? Note { get; set; }
    }

    [Obsolete("Sử dụng ShiftTaskAssignment thay thế cho TaskItem")]
    public class TaskItem : ShiftTaskAssignment
    {
    }
}
