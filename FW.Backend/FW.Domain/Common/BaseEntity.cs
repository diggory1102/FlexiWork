namespace FW.Domain.Common;

/// <summary>
/// Lớp cơ sở cho mọi Entity định danh trong hệ thống FlexiWork.
/// </summary>
public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;
}

public abstract class BaseEntity : BaseEntity<int>
{
}

