namespace FW.Domain.Enums;

/// <summary>
/// Tên vai trò RBAC — khớp với cột roles.role_name và claim Role trong JWT.
/// </summary>
public static class RoleNames
{
    /// <summary>Quản trị viên toàn hệ thống.</summary>
    public const string Admin = "ROLE_ADMIN";

    /// <summary>Bộ phận Nhân sự, phạm vi toàn doanh nghiệp.</summary>
    public const string Hr = "ROLE_HR";

    /// <summary>Quản lý chi nhánh / Cửa hàng trưởng, phạm vi theo chi nhánh của mình (users.branch_id).</summary>
    public const string Manager = "ROLE_MANAGER";

    /// <summary>Nhân viên, chỉ thao tác trên dữ liệu cá nhân.</summary>
    public const string Employee = "ROLE_EMPLOYEE";

    // Nhóm vai trò dùng trực tiếp trong [Authorize(Roles = ...)] (danh sách cách nhau bởi dấu phẩy)

    /// <summary>Admin hoặc HR — quản trị toàn doanh nghiệp.</summary>
    public const string AdminOrHr = Admin + "," + Hr;

    /// <summary>Admin, HR hoặc Manager — các vai trò quản lý (Manager bị giới hạn theo chi nhánh).</summary>
    public const string Managers = Admin + "," + Hr + "," + Manager;

    public static readonly IReadOnlyList<string> All = new[] { Admin, Hr, Manager, Employee };

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
