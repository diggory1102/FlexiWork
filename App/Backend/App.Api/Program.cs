using App.Application.Interfaces.Data;
using App.Application.Interfaces.Repositories;
using App.Infrastructure.Data;
using App.Infrastructure.Data.TypeHandlers;
using App.Infrastructure.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// 1. CẤU HÌNH KẾT NỐI DATABASE & DAPPER
// =============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chuỗi kết nối 'DefaultConnection' chưa được cấu hình.");

// Đăng ký Entity Framework Core DbContext (MySQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 32))));

// Đăng ký Dapper Connection Factory (Micro-ORM hiệu năng cao)
builder.Services.AddSingleton<IDbConnectionFactory>(new MySqlConnectionFactory(connectionString));

// Đăng ký Dapper Custom Type Handlers (Xử lý vector Face ID JSON float[])
SqlMapper.AddTypeHandler(new JsonTypeHandler<float[]>());
SqlMapper.AddTypeHandler(new JsonTypeHandler<List<float>>());

// =============================================================================
// 2. ĐĂNG KÝ DEPENDENCY INJECTION (REPOSITORIES & SERVICES)
// =============================================================================
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IBranchRepository, BranchRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<App.Application.Interfaces.Services.IAttendanceService, App.Application.Services.AttendanceService>();

// Đăng ký Service giao tiếp AI Microservice
builder.Services.AddHttpClient<App.Application.Interfaces.Services.IFaceRecognitionService, App.Infrastructure.Services.FaceRecognitionService>();

// =============================================================================
// 3. CONTROLLERS & SWAGGER UI
// =============================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// =============================================================================
// 4. HTTP REQUEST PIPELINE
// =============================================================================
// Kích hoạt Swagger UI (Hiển thị ngay tại root / hoặc /swagger)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlexiWork API v1");
    c.RoutePrefix = string.Empty; // Truy cập trực tiếp tại http://localhost:5265/
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
