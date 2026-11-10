using FW.Application.Common.Models;
using FW.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    private readonly IDbConnectionFactory _connectionFactory;

    public HealthCheckController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(ApiResponse<object>.SuccessResult(new
        {
            Status = "Healthy",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Framework = ".NET 8.0",
            SystemTime = DateTime.UtcNow
        }, "Hệ thống FlexiWork Backend đang hoạt động ổn định."));
    }
}

