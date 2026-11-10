using System.Net;

namespace FW.Application.Features.Auth;

/// <summary>
/// Nội dung email HTML của phân hệ xác thực.
/// </summary>
public static class AuthEmailTemplates
{
    public static string PasswordResetOtp(string fullName, string otp, int validMinutes) => $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#0f172a">
          <h2 style="color:#2563eb">FlexiWork</h2>
          <p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
          <p>Bạn vừa yêu cầu đặt lại mật khẩu. Mã OTP của bạn là:</p>
          <p style="font-size:28px;font-weight:bold;letter-spacing:6px;margin:16px 0">{otp}</p>
          <p>Mã có hiệu lực trong <strong>{validMinutes} phút</strong> và chỉ dùng được một lần.</p>
          <p style="color:#64748b;font-size:13px">Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu của bạn vẫn an toàn.</p>
        </div>
        """;
}
