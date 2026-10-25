using System;
using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Tài khoản nhân viên & thông tin định danh
    /// </summary>
    public class User : BaseEntity
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int? PositionId { get; set; }
        public Position? Position { get; set; }

        public string Status { get; set; } = "active";

        // Navigation Properties
        public FaceProfile? FaceProfile { get; set; }
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
        public ICollection<ShiftTaskAssignment> AssignedTasks { get; set; } = new List<ShiftTaskAssignment>();
        public ICollection<ConversationMember> ConversationMembers { get; set; } = new List<ConversationMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<MonthlyTimesheet> MonthlyTimesheets { get; set; } = new List<MonthlyTimesheet>();
    }
}
