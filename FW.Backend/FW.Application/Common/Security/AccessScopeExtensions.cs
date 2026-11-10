using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Common.Security;

/// <summary>
/// Phân quyền theo PHẠM VI DỮ LIỆU (bổ sung cho phân quyền theo vai trò ở Controller):
/// - Admin, HR: toàn doanh nghiệp.
/// - Manager: chỉ chi nhánh của mình (users.branch_id).
/// - Employee: không có quyền quản trị chi nhánh (chỉ dữ liệu cá nhân, kiểm tra riêng ở từng nghiệp vụ).
/// </summary>
public static class AccessScopeExtensions
{
    public static bool HasCompanyWideAccess(this ICurrentUserService user) =>
        user.IsInRole(RoleNames.Admin) || user.IsInRole(RoleNames.Hr);

    public static bool IsBranchManager(this ICurrentUserService user) =>
        user.IsInRole(RoleNames.Manager) && !user.HasCompanyWideAccess();

    /// <summary>
    /// Phạm vi chi nhánh dùng để lọc danh sách: null = toàn doanh nghiệp, có giá trị = chỉ chi nhánh đó.
    /// </summary>
    /// <exception cref="ForbiddenException">Người dùng không có quyền xem dữ liệu cấp chi nhánh.</exception>
    public static int? GetBranchScope(this ICurrentUserService user)
    {
        if (user.HasCompanyWideAccess()) return null;
        if (user.IsBranchManager() && user.BranchId.HasValue) return user.BranchId.Value;

        throw new ForbiddenException("Bạn không có quyền truy cập dữ liệu của chi nhánh.");
    }

    public static bool CanAccessBranch(this ICurrentUserService user, int? branchId)
    {
        if (user.HasCompanyWideAccess()) return true;
        return user.IsBranchManager() && branchId.HasValue && user.BranchId == branchId;
    }

    /// <exception cref="ForbiddenException">Chi nhánh nằm ngoài phạm vi quản lý của người dùng.</exception>
    public static void EnsureCanAccessBranch(this ICurrentUserService user, int? branchId)
    {
        if (!user.CanAccessBranch(branchId))
        {
            throw new ForbiddenException("Bạn chỉ được thao tác trên dữ liệu thuộc chi nhánh mình quản lý.");
        }
    }
}
