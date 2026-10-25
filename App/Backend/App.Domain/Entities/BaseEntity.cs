using System;

namespace App.Domain.Entities
{
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
    }

    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Base Entity hỗ trợ kiểu khóa chính generic (int, long, Guid...)
    /// </summary>
    public abstract class BaseEntity<TKey> : IAuditableEntity, ISoftDeletable
    {
        public TKey Id { get; set; } = default!;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Base Entity mặc định với Khóa chính kiểu int
    /// </summary>
    public abstract class BaseEntity : BaseEntity<int>
    {
    }
}
