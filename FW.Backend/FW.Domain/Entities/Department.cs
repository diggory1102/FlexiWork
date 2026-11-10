using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Phòng ban trong cơ cấu tổ chức (bảng departments).
/// </summary>
public class Department : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
