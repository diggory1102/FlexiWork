using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

/// <summary>Các vai trò quản lý dưới dạng mảng (RoleNames.Managers của backend là chuỗi cho [Authorize]).</summary>
public static class AiRoles
{
    public static readonly string[] Managers = { RoleNames.Admin, RoleNames.Hr, RoleNames.Manager };
}
