using System.Net;

namespace FW.Application.Features.Users;

/// <summary>
/// Nội dung email của phân hệ quản lý nhân sự.
/// </summary>
public static class UserEmailTemplates
{
    public static string Invitation(string fullName, string email, string temporaryPassword) =>
        Wrap(fullName,
            "Tài khoản FlexiWork của bạn đã được tạo. Thông tin đăng nhập:",
            email, temporaryPassword);

    public static string PasswordReissued(string fullName, string email, string temporaryPassword) =>
        Wrap(fullName,
            "Bộ phận Nhân sự vừa cấp lại mật khẩu cho tài khoản FlexiWork của bạn. Thông tin đăng nhập mới:",
            email, temporaryPassword);

    private static string Wrap(string fullName, string intro, string email, string temporaryPassword) => $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#0f172a">
          <h2 style="color:#2563eb">FlexiWork</h2>
          <p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
          <p>{intro}</p>
          <table style="margin:12px 0;font-size:15px">
            <tr><td style="padding:4px 12px 4px 0;color:#64748b">Email</td><td><strong>{WebUtility.HtmlEncode(email)}</strong></td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#64748b">Mật khẩu tạm</td><td><strong style="font-family:monospace;font-size:17px">{WebUtility.HtmlEncode(temporaryPassword)}</strong></td></tr>
          </table>
          <p>Vì lý do bảo mật, hệ thống sẽ <strong>yêu cầu bạn đổi mật khẩu</strong> ngay ở lần đăng nhập đầu tiên.</p>
          <p style="color:#64748b;font-size:13px">Không chia sẻ email này cho bất kỳ ai.</p>
        </div>
        """;
}
