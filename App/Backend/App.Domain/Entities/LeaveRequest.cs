using System;

namespace App.Domain.Entities
{
    /// <summary>
    /// Đơn xin nghỉ phép / Yêu cầu phê duyệt (Nghỉ ốm, Đổi ca, Đi muộn, OT)
    /// </summary>
    public class LeaveRequest : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int LeaveTypeId { get; set; }
        public LeaveType LeaveType { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal DurationDays { get; set; } = 1.0m;

        // Dành cho đơn đổi ca
        public int? TargetUserId { get; set; }
        public User? TargetUser { get; set; }

        public long? SourceScheduleId { get; set; }
        public Schedule? SourceSchedule { get; set; }

        public long? TargetScheduleId { get; set; }
        public Schedule? TargetSchedule { get; set; }

        public string Reason { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public string Status { get; set; } = "pending";

        public int? ApprovedBy { get; set; }
        public User? Approver { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApproverNote { get; set; }
    }
}
