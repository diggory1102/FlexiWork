using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Vai trò bảo mật RBAC (bảng roles).
/// </summary>
public class Role : BaseEntity
{
    /// <summary>Giá trị thuộc <see cref="RoleNames"/>.</summary>
    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }
}
