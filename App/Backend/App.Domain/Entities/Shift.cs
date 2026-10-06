using System;
using System.Collections.Generic;
namespace App.Domain.Entities
{
    public class Shift : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
