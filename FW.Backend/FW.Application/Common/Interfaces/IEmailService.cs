namespace FW.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ gửi email (mời kích hoạt tài khoản, OTP, thông báo hệ thống).
/// </summary>
public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
