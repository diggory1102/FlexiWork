namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình máy chủ gửi email SMTP dùng cho MailKit (section "Smtp").
/// </summary>
public class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderName { get; set; } = "FlexiWork";
    public string SenderEmail { get; set; } = string.Empty;
}
