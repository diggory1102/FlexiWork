using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Infrastructure.Services.Geocoding;

/// <summary>
/// Tra tọa độ qua Nominatim (OpenStreetMap). Giữ đúng giới hạn 1 yêu cầu/giây của dịch vụ miễn phí bằng cách
/// xếp hàng các yêu cầu trong toàn ứng dụng (kết quả đã được cache ở tầng Application nên hiếm khi phải gọi).
/// </summary>
public class NominatimGeocodingService : IGeocodingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1100);
    private static DateTime _lastCallUtc = DateTime.MinValue;

    private readonly HttpClient _http;
    private readonly GeocodingSettings _settings;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(HttpClient http, IOptions<GeocodingSettings> settings, ILogger<NominatimGeocodingService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeocodeResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var url = $"search?format=jsonv2&addressdetails=0&accept-language=vi&limit={limit}&q={Uri.EscapeDataString(query)}";
        if (!string.IsNullOrWhiteSpace(_settings.CountryCodes)) url += $"&countrycodes={Uri.EscapeDataString(_settings.CountryCodes)}";
        if (!string.IsNullOrWhiteSpace(_settings.ContactEmail)) url += $"&email={Uri.EscapeDataString(_settings.ContactEmail)}";

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var wait = _lastCallUtc + MinInterval - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);

            List<NominatimPlace>? places;
            try
            {
                places = await _http.GetFromJsonAsync<List<NominatimPlace>>(url, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                _logger.LogWarning(ex, "Nominatim không phản hồi khi tra địa chỉ");
                throw new ExternalServiceException("Dịch vụ bản đồ đang bận, chưa tra được tọa độ. Vui lòng thử lại sau hoặc nhập tọa độ thủ công.");
            }
            finally
            {
                _lastCallUtc = DateTime.UtcNow;
            }

            var results = new List<GeocodeResult>();
            foreach (var place in places ?? new List<NominatimPlace>())
            {
                if (double.TryParse(place.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(place.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                {
                    results.Add(new GeocodeResult(place.DisplayName ?? string.Empty, lat, lng, place.Category, place.Type));
                }
            }
            return results;
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed class NominatimPlace
    {
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
