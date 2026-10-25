using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Phòng ban công ty
    /// </summary>
    public class Department : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Navigation Properties
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
