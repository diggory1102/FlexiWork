using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Danh mục phân loại đơn từ
    /// </summary>
    public class LeaveType : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public bool IsPaid { get; set; } = true;
        public bool RequiresAttachment { get; set; } = false;

        // Navigation Properties
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    }
}
