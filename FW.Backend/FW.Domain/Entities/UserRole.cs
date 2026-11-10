namespace FW.Domain.Entities;

/// <summary>
/// Liên kết N-N giữa người dùng và vai trò (bảng user_roles, khóa chính kép user_id + role_id).
/// </summary>
public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; }
}
