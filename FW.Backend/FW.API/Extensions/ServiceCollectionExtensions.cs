using System.Text;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Application.Common.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace FW.API.Extensions;

/// <summary>
/// Các extension method đăng ký dịch vụ riêng của tầng API (Presentation).
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "FlexiWorkCors";

    /// <summary>
    /// Xác thực JWT Bearer. Hỗ trợ đọc token từ query string "access_token" cho kết nối SignalR (/hubs/*).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);

        var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "JwtSettings:Secret chưa được cấu hình hoặc ngắn hơn 32 ký tự. " +
                "Hãy tạo appsettings.Development.json từ appsettings.Development.example.json, " +
                "hoặc đặt biến môi trường JwtSettings__Secret.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Giữ nguyên tên claim trong token (sub, role, branch_id...) — không tự đổi sang URI dài của .NET
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = AppClaimTypes.Name,
                    RoleClaimType = AppClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    // Trả lỗi 401/403 theo định dạng ApiResponse thống nhất thay vì body rỗng
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteApiErrorAsync(context.Response, StatusCodes.Status401Unauthorized,
                            "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.");
                    },
                    OnForbidden = context =>
                        WriteApiErrorAsync(context.Response, StatusCodes.Status403Forbidden,
                            "Bạn không có quyền thực hiện thao tác này.")
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Lỗi kiểm tra dữ liệu đầu vào (DataAnnotations) trả về ApiResponse thay vì ProblemDetails mặc định.
    /// </summary>
    public static IMvcBuilder ConfigureValidationResponse(this IMvcBuilder builder)
    {
        return builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Giá trị không hợp lệ." : e.ErrorMessage)
                    .Distinct()
                    .ToList();
                return new BadRequestObjectResult(ApiResponse.FailureResult("Dữ liệu gửi lên không hợp lệ.", errors));
            };
        });
    }

    private static Task WriteApiErrorAsync(HttpResponse response, int statusCode, string message)
    {
        if (response.HasStarted) return Task.CompletedTask;
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(ApiResponse.FailureResult(message), ApiJson.Options);
    }

    /// <summary>
    /// CORS: nếu khai báo "Cors:AllowedOrigins" thì chỉ cho phép các origin đó (kèm credentials cho SignalR),
    /// ngược lại cho phép mọi origin (chỉ nên dùng khi phát triển).
    /// </summary>
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Swagger/OpenAPI hỗ trợ nhập JWT Bearer.
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FlexiWork API",
                Version = "v1",
                Description = "Hệ thống Quản lý Ca kíp & Chấm công Đa lớp FlexiWork (DATN)"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhập token JWT (không cần tiền tố \"Bearer \")"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
