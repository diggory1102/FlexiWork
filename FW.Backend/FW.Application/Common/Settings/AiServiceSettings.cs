namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình kết nối tới FW.AiService (section "AiService").
/// </summary>
public class AiServiceSettings
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 30;
}
