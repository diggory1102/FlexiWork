using FW.API.Extensions;
using FW.API.Middlewares;
using FW.API.Services;
using FW.Application;
using FW.Application.Common.Interfaces;
using FW.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers (URL chữ thường: /api/auth/login) + lỗi validate trả về ApiResponse
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Encoder = ApiJson.Encoder)
    .ConfigureValidationResponse();

// 2. Tầng Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

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

// Global Exception Middleware phải đặt ở đầu pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlexiWork API v1");
        c.RoutePrefix = string.Empty; // Swagger hiển thị tại URL gốc (/)
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(ServiceCollectionExtensions.CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHub<NotificationHub>("/hubs/notifications");  // Task 5.1
// app.MapHub<ChatHub>("/hubs/chat");                    // Task 5.1

app.Run();
