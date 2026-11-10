namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình tài khoản Cloudinary lưu trữ ảnh (section "Cloudinary").
/// </summary>
public class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    /// <summary>Thư mục gốc chứa ảnh của hệ thống trên Cloudinary.</summary>
    public string RootFolder { get; set; } = "flexiwork";
}
