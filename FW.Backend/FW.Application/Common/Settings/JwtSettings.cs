namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình phát hành & xác thực JWT (section "JwtSettings").
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 1440;
}
