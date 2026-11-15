namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình tra tọa độ từ địa chỉ (section "Geocoding") — dùng Nominatim của OpenStreetMap (miễn phí, tối đa 1 yêu cầu/giây,
/// phải gửi User-Agent nhận diện ứng dụng: https://operations.osmfoundation.org/policies/nominatim/).
/// </summary>
public class GeocodingSettings
{
    public const string SectionName = "Geocoding";

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>Tên ứng dụng gửi kèm mỗi yêu cầu (bắt buộc theo chính sách Nominatim).</summary>
    public string UserAgent { get; set; } = "FlexiWork-HRM/1.0";

    /// <summary>Email liên hệ gửi kèm (khuyến nghị khi dùng nhiều); để trống thì không gửi.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Giới hạn tìm trong các quốc gia (mã ISO, cách nhau bởi dấu phẩy).</summary>
    public string CountryCodes { get; set; } = "vn";

    public int TimeoutSeconds { get; set; } = 10;
}
