using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Chức vụ và đơn giá lương giờ cơ sở
    /// </summary>
    public class Position : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public decimal BaseSalaryPerHour { get; set; } = 0.00m;
        public string? Description { get; set; }

        // Navigation Properties
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
