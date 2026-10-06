using System;
using App.Domain.Enums;
namespace App.Domain.Entities
{
    public class Schedule : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int ShiftId { get; set; }
        public Shift Shift { get; set; } = null!;
        public DateTime WorkDate { get; set; }
        public RequestStatus Status { get; set; }
        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }
    }
}
