using System.ComponentModel.DataAnnotations;

namespace FW.Application.Features.Users;

/// <summary>Hồ sơ nhân sự trả về client (không bao giờ chứa password_hash).</summary>
public record UserDto(
    int Id,
    string EmployeeCode,
    string Email,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    int? PositionId,
    string? PositionTitle,
    string EmploymentType,
    DateOnly? HireDate,
    string Status,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Kết quả tạo tài khoản: kèm cờ cho biết email mời đã gửi được hay chưa.</summary>
public record CreateUserResult(UserDto User, bool InvitationSent);

/// <summary>Thông tin hồ sơ dùng chung cho tạo mới và cập nhật.</summary>
public class UserProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã nhân viên.")]
    [StringLength(20, ErrorMessage = "Mã nhân viên tối đa 20 ký tự.")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(120, ErrorMessage = "Email tối đa 120 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(15, ErrorMessage = "Số điện thoại tối đa 15 ký tự.")]
    [RegularExpression(@"^[0-9+ ]*$", ErrorMessage = "Số điện thoại chỉ gồm chữ số, dấu + và khoảng trắng.")]
    public string? PhoneNumber { get; set; }

    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }

    /// <summary>full_time / part_time. Bỏ trống = full_time.</summary>
    public string? EmploymentType { get; set; }

    public DateOnly? HireDate { get; set; }
}

public class CreateUserRequest : UserProfileRequest
{
    /// <summary>Danh sách vai trò (RoleNames). Bỏ trống = ROLE_EMPLOYEE.</summary>
    public List<string>? Roles { get; set; }
}

public class UpdateUserStatusRequest
{
    /// <summary>active / suspended / left.</summary>
    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    public string Status { get; set; } = string.Empty;
}

public class UpdateUserRolesRequest
{
    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [MinLength(1, ErrorMessage = "Người dùng phải có ít nhất một vai trò.")]
    public List<string> Roles { get; set; } = new();
}

/// <summary>Thông tin nhân viên được tự cập nhật (họ tên, phòng ban... do HR quản lý).</summary>
public class UpdateMyProfileRequest
{
    [StringLength(15, ErrorMessage = "Số điện thoại tối đa 15 ký tự.")]
    [RegularExpression(@"^[0-9+ ]*$", ErrorMessage = "Số điện thoại chỉ gồm chữ số, dấu + và khoảng trắng.")]
    public string? PhoneNumber { get; set; }
}
