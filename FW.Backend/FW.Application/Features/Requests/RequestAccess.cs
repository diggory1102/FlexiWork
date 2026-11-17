using FW.Application.Common.Interfaces;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Requests;

/// <summary>Quyền trên đơn từ theo vai trò và phạm vi chi nhánh.</summary>
public static class RequestAccess
{
    /// <summary>Admin/HR: mọi đơn; Quản lý: đơn thuộc chi nhánh mình.</summary>
    public static bool CanManage(ICurrentUserService user, int? branchId) =>
        user.HasCompanyWideAccess() || (user.IsBranchManager() && user.CanAccessBranch(branchId));

    /// <summary>
    /// Duyệt được: đơn đang chờ duyệt, trong phạm vi quản lý, không phải đơn của chính mình
    /// (đơn của Quản lý do HR/Admin duyệt; đơn của HR do HR khác hoặc Admin duyệt).
    /// </summary>
    public static bool CanReview(ICurrentUserService user, LeaveRequest request) =>
        request.Status == RequestStatus.Pending && request.UserId != user.UserId && CanManage(user, request.BranchId);
}
