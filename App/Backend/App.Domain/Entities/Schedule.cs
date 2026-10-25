using System;
using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Lịch phân ca chi tiết theo ngày
    /// </summary>
    public class Schedule : BaseEntity<long>
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ShiftId { get; set; }
        public Shift Shift { get; set; } = null!;

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public DateTime WorkDate { get; set; }
        public string Status { get; set; } = "scheduled";

        // Navigation Properties
        public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
        public ICollection<ShiftTaskAssignment> ShiftTaskAssignments { get; set; } = new List<ShiftTaskAssignment>();
    }
}
