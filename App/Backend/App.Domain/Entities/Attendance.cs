using System;
namespace App.Domain.Entities
{
    public class Attendance : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? CheckInIp { get; set; }
        public decimal? CheckInLatitude { get; set; }
        public decimal? CheckInLongitude { get; set; }
        public decimal ActualWorkDay { get; set; }
        public bool IsLate { get; set; }
        public int LateMinutes { get; set; }
    }
}
