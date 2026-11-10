using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using FW.Infrastructure.Database;
using FW.Infrastructure.Services.Caching;
using FW.Infrastructure.Services.Email;
using FW.Infrastructure.Services.FaceRecognition;
using FW.Infrastructure.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FW.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ thuộc tầng Hạ tầng (Infrastructure Layer).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Cấu hình (Options pattern)
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        services.Configure<AiServiceSettings>(configuration.GetSection(AiServiceSettings.SectionName));

        // 2. Database (MySQL + Dapper)
        DapperConfig.Configure();
        services.AddSingleton<IDbConnectionFactory, MySqlDbConnectionFactory>();

        // 3. Redis — chỉ kết nối khi được sử dụng lần đầu; không chặn khởi động nếu Redis chưa chạy
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
            var options = ConfigurationOptions.Parse(settings.ConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<ICacheService, RedisCacheService>();

        // 4. Email (MailKit + SMTP)
        services.AddTransient<IEmailService, SmtpEmailService>();

        // 5. Lưu trữ ảnh (Cloudinary)
        services.AddSingleton<IFileStorageService, CloudinaryStorageService>();

        // 6. Client gọi FW.AiService
        services.AddHttpClient<IFaceRecognitionService, FaceRecognitionClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<AiServiceSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        // 7. Repositories (Dapper) — đăng ký tại đây khi triển khai từ Task 1.2

        return services;
    }
}
