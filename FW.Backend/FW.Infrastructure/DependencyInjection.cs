using FW.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FW.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ thuộc tầng Hạ tầng (Infrastructure Layer).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký DbConnectionFactory dùng Singleton để quản lý chuỗi kết nối và cấp phát kết nối
        services.AddSingleton<IDbConnectionFactory, MySqlDbConnectionFactory>();

        return services;
    }
}

