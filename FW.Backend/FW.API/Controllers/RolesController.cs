using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Users;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Danh mục vai trò (phục vụ dropdown khi tạo tài khoản / phân quyền).
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
public class RolesController : ApiControllerBase
{
    private readonly IRoleRepository _roles;
    private readonly ICurrentUserService _currentUser;

    public RolesController(IRoleRepository roles, ICurrentUserService currentUser)
    {
        _roles = roles;
        _currentUser = currentUser;
    }

    /// <summary>Toàn bộ vai trò, kèm cờ "assignable" cho biết người dùng hiện tại có được gán vai trò đó không.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var assignable = UserManagementRules.AssignableRoles(_currentUser);
        var roles = await _roles.GetAllAsync(cancellationToken);
        return Success(roles.Select(r => new
        {
            r.Id,
            r.RoleName,
            r.Description,
            Assignable = assignable.Contains(r.RoleName)
        }));
    }
}
