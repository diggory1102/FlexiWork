using System.Text.RegularExpressions;
using FW.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace FW.Infrastructure.Services.Email;

/// <summary>
/// CHỈ DÙNG KHI PHÁT TRIỂN: thay vì gửi email thật, ghi nội dung email ra log console
/// (để lấy OTP khi chưa cấu hình SMTP). Không bao giờ được đăng ký ở môi trường Production.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var plainText = Regex.Replace(Regex.Replace(htmlBody, "<[^>]+>", " "), @"\s+", " ").Trim();
        _logger.LogWarning("[DEV EMAIL - chưa cấu hình SMTP] Tới: {To} | Tiêu đề: {Subject} | Nội dung: {Body}",
            toEmail, subject, plainText);
        return Task.CompletedTask;
    }
}
