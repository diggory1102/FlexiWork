using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Danh mục đầu việc chuẩn mẫu theo từng loại ca làm việc
    /// </summary>
    public class ShiftTask : BaseEntity
    {
        public int ShiftId { get; set; }
        public Shift Shift { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsMandatory { get; set; } = true;

        // Navigation Properties
        public ICollection<ShiftTaskAssignment> Assignments { get; set; } = new List<ShiftTaskAssignment>();
    }
}
