using System.Collections.Generic;
namespace App.Domain.Entities
{
    public class Role : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Permissions { get; set; } = string.Empty;
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
