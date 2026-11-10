namespace FW.Application.Common.Interfaces;

/// <summary>
/// Truy xuất thông tin người dùng đang đăng nhập từ JWT claims của request hiện tại.
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; }
    int? BranchId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
