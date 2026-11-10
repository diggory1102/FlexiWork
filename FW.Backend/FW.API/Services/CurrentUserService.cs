using System.Security.Claims;
using FW.Application.Common.Interfaces;

namespace FW.API.Services;

/// <summary>
/// Đọc thông tin người dùng hiện tại từ JWT claims (UserId, BranchId, Roles).
/// Tên claim "branch_id" sẽ được dùng thống nhất khi phát hành token ở Task 1.3.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public const string BranchIdClaim = "branch_id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId => ParseInt(User?.FindFirstValue(ClaimTypes.NameIdentifier));

    public int? BranchId => ParseInt(User?.FindFirstValue(BranchIdClaim));

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    private static int? ParseInt(string? value) => int.TryParse(value, out var result) ? result : null;
}
