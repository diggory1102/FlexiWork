using System;
using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Danh mục khung ca làm việc
    /// </summary>
    public class Shift : BaseEntity
    {
        public string ShiftCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int BreakMinutes { get; set; } = 0;
        public decimal WorkUnits { get; set; } = 1.00m;
        public bool IsNightShift { get; set; } = false;
        public bool IsCrossDay { get; set; } = false;

        // Navigation Properties
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public ICollection<ShiftTask> ShiftTasks { get; set; } = new List<ShiftTask>();
    }
}
