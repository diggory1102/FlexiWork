namespace FW.Application.Common.Security;

/// <summary>
/// Tên claim trong JWT của FlexiWork. Dùng tên ngắn theo chuẩn JWT và tắt cơ chế tự đổi tên claim
/// (MapInboundClaims = false) để phía phát hành và phía đọc token luôn khớp nhau.
/// </summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string BranchId = "branch_id";
    public const string EmployeeCode = "employee_code";
}
