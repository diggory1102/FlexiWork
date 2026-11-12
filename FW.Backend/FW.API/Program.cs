using FW.API.Extensions;
using FW.API.Middlewares;
using FW.API.Services;
using FW.Application;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Setup;
using Microsoft.AspNetCore.HttpOverrides;
using FW.Infrastructure;
using FW.Infrastructure.Services.Storage;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers (URL chữ thường: /api/auth/login) + lỗi validate trả về ApiResponse
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Encoder = ApiJson.Encoder)
    .ConfigureValidationResponse();

// 2. Tầng Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.Configure<BootstrapSettings>(builder.Configuration.GetSection(BootstrapSettings.SectionName));

// Chạy sau reverse proxy (Render, Docker): tin header X-Forwarded-* để biết request gốc là HTTPS
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 3. Người dùng hiện tại (đọc từ JWT claims)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// 4. Bảo mật: JWT Bearer + Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);

// 5. CORS & Swagger
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

// 6. SignalR (các Hub sẽ được map ở Task 5.1)
builder.Services.AddSignalR();

var app = builder.Build();

app.UseForwardedHeaders();

// Global Exception Middleware phải đặt ở đầu pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger: luôn bật khi phát triển; trên cloud chỉ bật khi Swagger:Enabled=true (VD: để demo API)
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlexiWork API v1");
        c.RoutePrefix = string.Empty; // Swagger hiển thị tại URL gốc (/)
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Tạo Quản trị viên đầu tiên từ biến môi trường Bootstrap__AdminEmail / Bootstrap__AdminPassword (nếu chưa có Admin)
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<IBootstrapAdminService>().EnsureAdminAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không khởi tạo được tài khoản Quản trị viên đầu tiên.");
    }
}

// Khi phát triển chưa có Cloudinary: phục vụ ảnh đã tải lên từ thư mục cục bộ tại /uploads
if (LocalFileStorageService.IsEnabled(app.Configuration, app.Environment.IsDevelopment()))
{
    var uploadsRoot = LocalFileStorageService.ResolveRootPath(app.Configuration);
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = LocalFileStorageService.RequestPath
    });
}

app.UseCors(ServiceCollectionExtensions.CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHub<NotificationHub>("/hubs/notifications");  // Task 5.1
// app.MapHub<ChatHub>("/hubs/chat");                    // Task 5.1

app.Run();
