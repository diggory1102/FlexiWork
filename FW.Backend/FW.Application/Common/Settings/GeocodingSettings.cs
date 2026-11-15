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

    /// <summary>
    /// API key Geoapify (gói miễn phí 3.000 yêu cầu/ngày) cho ô gợi ý địa điểm khi gõ — Nominatim cấm dùng cho gợi ý khi gõ.
    /// Chỉ đặt qua biến môi trường <c>Geocoding__GeoapifyApiKey</c> hoặc appsettings.Development.json (không commit).
    /// Để trống thì ô gợi ý báo chưa cấu hình, nút "Tìm theo địa chỉ" (Nominatim) vẫn dùng được.
    /// </summary>
    public string? GeoapifyApiKey { get; set; }

    public string GeoapifyBaseUrl { get; set; } = "https://api.geoapify.com/";
}
