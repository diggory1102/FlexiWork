namespace FW.Domain.Common;

/// <summary>
/// Lớp cơ sở mở rộng tích hợp định danh, thời gian kiểm toán và cờ xóa mềm.
/// </summary>
public abstract class BaseAuditableEntity<TKey> : BaseEntity<TKey>, IAuditableEntity, ISoftDelete
{
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public abstract class BaseAuditableEntity : BaseAuditableEntity<int>
{
}

