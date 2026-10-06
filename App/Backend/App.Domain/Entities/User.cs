using System.Collections.Generic;
namespace App.Domain.Entities
{
    public class User : BaseEntity
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public byte? ActiveKey { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;
        public decimal BaseSalary { get; set; }
        public bool IsActive { get; set; }
        
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Request> Requests { get; set; } = new List<Request>();
        public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
    }
}
