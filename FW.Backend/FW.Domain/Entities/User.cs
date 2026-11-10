using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Hồ sơ nhân sự kiêm tài khoản đăng nhập hệ thống (bảng users).
/// </summary>
public class User : BaseAuditableEntity
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu đã băm bằng BCrypt — không bao giờ trả về client.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>Chi nhánh trực thuộc; với Manager đây cũng là chi nhánh phụ trách.</summary>
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }

    /// <summary>Giá trị thuộc <see cref="UserStatus"/>.</summary>
    public string Status { get; set; } = UserStatus.Active;

    public bool IsActive => Status == UserStatus.Active && !IsDeleted;
}
