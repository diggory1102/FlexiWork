namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình kết nối Redis (section "Redis").
/// </summary>
public class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    /// <summary>Tiền tố khóa để tránh trùng với ứng dụng khác dùng chung Redis.</summary>
    public string InstanceName { get; set; } = "flexiwork:";
}
