using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Thông tin Chi nhánh & Tọa độ tâm Geofencing
    /// </summary>
    public class Branch : BaseEntity
    {
        public string BranchCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int Radius { get; set; } = 50; // mét
        public string Status { get; set; } = "active";

        // Navigation Properties
        public ICollection<BranchWifi> BranchWifis { get; set; } = new List<BranchWifi>();
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
    }
}
