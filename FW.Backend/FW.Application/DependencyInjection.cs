using FW.Application.Features.Auth;
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

        return services;
    }
}
