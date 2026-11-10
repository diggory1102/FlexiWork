using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Users;

/// <summary>
/// Quy tắc phân quyền khi quản lý tài khoản nhân sự:
/// - Admin: gán mọi vai trò, quản lý mọi tài khoản.
/// - HR: chỉ gán Employee / Manager; không quản lý tài khoản có vai trò Admin.
/// - Không ai tự khóa, tự xóa hay tự đổi vai trò của chính mình (tránh tự mất quyền quản trị).
/// </summary>
public static class UserManagementRules
{
    public static IReadOnlyList<string> AssignableRoles(ICurrentUserService currentUser)
    {
        if (currentUser.IsInRole(RoleNames.Admin)) return RoleNames.All;
        if (currentUser.IsInRole(RoleNames.Hr)) return new[] { RoleNames.Employee, RoleNames.Manager };
        return Array.Empty<string>();
    }

    /// <exception cref="ForbiddenException">Có vai trò nằm ngoài quyền được gán.</exception>
    public static void EnsureCanAssign(ICurrentUserService currentUser, IEnumerable<string> roles)
    {
        var allowed = AssignableRoles(currentUser);
        var denied = roles.Where(r => !allowed.Contains(r)).ToList();
        if (denied.Count > 0)
        {
            throw new ForbiddenException($"Bạn không có quyền gán vai trò: {string.Join(", ", denied)}.");
        }
    }

    /// <exception cref="ForbiddenException">Người dùng hiện tại không được quản lý tài khoản đích.</exception>
    public static void EnsureCanManage(ICurrentUserService currentUser, int targetUserId, IReadOnlyList<string> targetRoles)
    {
        if (currentUser.IsInRole(RoleNames.Admin)) return;

        if (currentUser.IsInRole(RoleNames.Hr) && !targetRoles.Contains(RoleNames.Admin)) return;

        throw new ForbiddenException("Bạn không có quyền quản lý tài khoản này.");
    }

    /// <exception cref="BadRequestException">Thao tác lên chính tài khoản đang đăng nhập.</exception>
    public static void EnsureNotSelf(ICurrentUserService currentUser, int targetUserId, string action)
    {
        if (currentUser.UserId == targetUserId)
        {
            throw new BadRequestException($"Bạn không thể {action} chính tài khoản của mình.");
        }
    }
}
