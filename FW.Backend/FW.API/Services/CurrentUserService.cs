using System.Security.Claims;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Security;

namespace FW.API.Services;

/// <summary>
/// Đọc thông tin người dùng hiện tại từ JWT claims (xem <see cref="AppClaimTypes"/>).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId => ParseInt(User?.FindFirstValue(AppClaimTypes.UserId));

    public int? BranchId => ParseInt(User?.FindFirstValue(AppClaimTypes.BranchId));

    public IReadOnlyList<string> Roles =>
        User?.FindAll(AppClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    private static int? ParseInt(string? value) => int.TryParse(value, out var result) ? result : null;
}
