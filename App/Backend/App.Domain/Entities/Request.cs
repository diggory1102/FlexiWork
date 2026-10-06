using System;
using App.Domain.Enums;
namespace App.Domain.Entities
{
    public class Request : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public RequestType Type { get; set; }
        public string? LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public RequestStatus Status { get; set; }
        public string? RejectReason { get; set; }
        public int? ApproverId { get; set; }
        public User? Approver { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
