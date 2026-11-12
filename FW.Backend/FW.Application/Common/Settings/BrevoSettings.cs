namespace FW.Application.Common.Settings;

/// <summary>
/// Gửi email qua HTTP API của Brevo (section "Brevo") — dùng khi nền tảng triển khai chặn cổng SMTP
/// (VD: gói miễn phí của Render). Có ApiKey thì được ưu tiên hơn SMTP.
/// </summary>
public class BrevoSettings
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Email người gửi — phải được xác minh trong tài khoản Brevo.</summary>
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "FlexiWork";
    public string BaseUrl { get; set; } = "https://api.brevo.com/v3/";
}
