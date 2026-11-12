using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Settings;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Security;
using FW.Infrastructure.Services;
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
    /// <param name="isDevelopment">
    /// Môi trường phát triển: cho phép thay Redis bằng bộ nhớ trong và ghi email ra log khi chưa cấu hình SMTP.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        // 1. Cấu hình (Options pattern)
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.Configure<BrevoSettings>(configuration.GetSection(BrevoSettings.SectionName));
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        services.Configure<AiServiceSettings>(configuration.GetSection(AiServiceSettings.SectionName));

        // 2. Database (MySQL + Dapper) — mỗi request dùng chung 1 DbSession (kết nối + transaction)
        DapperConfig.Configure();
        services.AddSingleton<IDbConnectionFactory, MySqlDbConnectionFactory>();
        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DbSession>());
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // 3. Cache: Redis; khi phát triển mà để trống Redis:ConnectionString thì dùng bộ nhớ trong
        var redisConnection = configuration[$"{RedisSettings.SectionName}:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException("Chưa cấu hình Redis:ConnectionString.");
            }
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }
        else
        {
            // Chỉ kết nối khi được dùng lần đầu; không chặn khởi động nếu Redis tạm thời chưa sẵn sàng
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(RedisConnectionParser.Parse(redisConnection)));
            services.AddSingleton<ICacheService, RedisCacheService>();
        }

        // 4. Email — thứ tự ưu tiên: Brevo HTTP API (cloud chặn SMTP) → MailKit + SMTP → ghi log (chỉ khi phát triển)
        var brevoApiKey = configuration[$"{BrevoSettings.SectionName}:ApiKey"];
        var smtpHost = configuration[$"{SmtpSettings.SectionName}:Host"];
        if (!string.IsNullOrWhiteSpace(brevoApiKey))
        {
            services.AddHttpClient<IEmailService, BrevoEmailService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<BrevoSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("api-key", settings.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(20);
            });
        }
        else if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            services.AddTransient<IEmailService, SmtpEmailService>();
        }
        else if (isDevelopment)
        {
            services.AddTransient<IEmailService, LoggingEmailService>();
        }
        else
        {
            throw new InvalidOperationException("Chưa cấu hình gửi email: đặt Brevo:ApiKey (khuyên dùng trên Render) hoặc Smtp:Host.");
        }

        // 5. Bảo mật: băm mật khẩu & phát hành JWT (JwtSettings được bind ở tầng API)
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // 6. Lưu trữ ảnh: Cloudinary; khi phát triển mà chưa cấu hình Cloudinary thì lưu vào thư mục cục bộ
        if (LocalFileStorageService.IsEnabled(configuration, isDevelopment))
        {
            var publicBaseUrl = configuration["Storage:PublicBaseUrl"] ?? "http://localhost:5010";
            services.AddSingleton<IFileStorageService>(
                new LocalFileStorageService(LocalFileStorageService.ResolveRootPath(configuration), publicBaseUrl));
        }
        else
        {
            services.AddSingleton<IFileStorageService, CloudinaryStorageService>();
        }

        // 7. Client gọi FW.AiService
        services.AddHttpClient<IFaceRecognitionService, FaceRecognitionClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<AiServiceSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        // 8. Repositories (Dapper)
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IBranchWifiRepository, BranchWifiRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
