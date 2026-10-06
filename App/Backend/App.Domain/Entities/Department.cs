using System.Collections.Generic;
namespace App.Domain.Entities
{
    public class Department : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int? ManagerId { get; set; }
        public User? Manager { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
