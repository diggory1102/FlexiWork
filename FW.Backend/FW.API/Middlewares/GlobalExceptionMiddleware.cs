using System.Net;
using System.Text.Json;
using FW.API.Extensions;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Models;

namespace FW.API.Middlewares;

/// <summary>
/// Middleware bắt lỗi toàn cục, chuẩn hóa định dạng trả về ApiResponse.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        HttpStatusCode statusCode = HttpStatusCode.InternalServerError;
        string message = "Đã xảy ra lỗi hệ thống. Vui lòng liên hệ quản trị viên.";
        List<string>? errors = null;

        if (exception is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            message = appEx.Message;
            errors = appEx.Errors;
            _logger.LogWarning(appEx, "AppException ({StatusCode}): {Message}", statusCode, message);
        }
        else
        {
            _logger.LogError(exception, "Unhandled Exception: {Message}", exception.Message);
#if DEBUG
            errors = new List<string> { exception.Message, exception.StackTrace ?? string.Empty };
#endif
        }

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse.FailureResult(message, errors);
        var json = JsonSerializer.Serialize(response, ApiJson.Options);
        await context.Response.WriteAsync(json);
    }
}

