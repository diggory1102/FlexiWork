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

    /// <summary>Loại hợp đồng, giá trị thuộc <see cref="Enums.EmploymentType"/>.</summary>
    public string EmploymentType { get; set; } = Enums.EmploymentType.FullTime;

    /// <summary>Ngày vào làm (chỉ dùng phần ngày).</summary>
    public DateTime? HireDate { get; set; }

    /// <summary>Giá trị thuộc <see cref="UserStatus"/>.</summary>
    public string Status { get; set; } = UserStatus.Active;

    /// <summary>Đang dùng mật khẩu tạm do hệ thống cấp — bắt buộc đổi ở lần đăng nhập tới.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Thời điểm (UTC, làm tròn tới giây) mật khẩu được đổi gần nhất; null nếu chưa từng đổi.
    /// JWT phát hành trước thời điểm này bị từ chối — đổi mật khẩu sẽ đăng xuất các thiết bị khác.
    /// </summary>
    public DateTime? PasswordChangedAt { get; set; }

    public bool IsActive => Status == UserStatus.Active && !IsDeleted;
}
