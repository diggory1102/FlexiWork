using FW.Application.Features.Auth;
using FW.Application.Features.Organization;
using FW.Application.Features.Scheduling;
using FW.Application.Features.Setup;
using FW.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace FW.Application;

/// <summary>
/// Đăng ký các dịch vụ thuộc tầng Ứng dụng (Application Layer).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionStampService, SessionStampService>();

        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();

        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IAttendancePolicyService, AttendancePolicyService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();

        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();

        return services;
    }
}
