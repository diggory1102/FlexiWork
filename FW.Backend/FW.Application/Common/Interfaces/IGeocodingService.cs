namespace FW.Application.Common.Interfaces;

/// <summary>Một vị trí tìm được từ địa chỉ dạng chữ.</summary>
/// <param name="DisplayName">Tên đầy đủ của vị trí do dịch vụ bản đồ trả về.</param>
/// <param name="Category">Loại vị trí (VD: building, highway, place) — để biết mức độ chính xác.</param>
public record GeocodeResult(string DisplayName, double Latitude, double Longitude, string? Category, string? Type);

/// <summary>Gợi ý địa điểm khi đang gõ (autocomplete) — Geoapify.</summary>
public interface IPlaceSuggestionService
{
    /// <summary>Đã cấu hình API key chưa.</summary>
    bool IsAvailable { get; }

    /// <param name="biasLatitude">Ưu tiên kết quả gần vị trí này (VD tâm hiện tại của chi nhánh).</param>
    /// <exception cref="Exceptions.ExternalServiceException">Dịch vụ không phản hồi / hết hạn mức.</exception>
    Task<IReadOnlyList<GeocodeResult>> SuggestAsync(string text, double? biasLatitude, double? biasLongitude, int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Tra tọa độ từ địa chỉ (geocoding) qua dịch vụ bản đồ bên ngoài.</summary>
public interface IGeocodingService
{
    /// <exception cref="Exceptions.ExternalServiceException">Dịch vụ bản đồ không phản hồi.</exception>
    Task<IReadOnlyList<GeocodeResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);
}
