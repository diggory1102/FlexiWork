using Microsoft.Extensions.DependencyInjection;

namespace FW.Application;

/// <summary>
/// Đăng ký các dịch vụ thuộc tầng Ứng dụng (Application Layer).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Sẽ đăng ký AutoMapper, MediatR/FluentValidation hoặc Domain Services tại đây khi triển khai
        return services;
    }
}

