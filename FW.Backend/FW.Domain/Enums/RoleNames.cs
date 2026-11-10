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

    public static readonly IReadOnlyList<string> All = new[] { Admin, Hr, Manager, Employee };

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
