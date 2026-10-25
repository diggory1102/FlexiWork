using System.Collections.Generic;

namespace App.Domain.Entities
{
    /// <summary>
    /// Vai trò bảo mật (Role-based Access Control)
    /// </summary>
    public class Role : BaseEntity
    {
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Navigation Properties
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
