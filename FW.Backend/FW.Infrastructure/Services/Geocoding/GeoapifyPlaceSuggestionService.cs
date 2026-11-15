using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Infrastructure.Services.Geocoding;

/// <summary>
/// Gợi ý địa điểm khi gõ qua Geoapify Address Autocomplete (dữ liệu OpenStreetMap; gói miễn phí 3.000 yêu cầu/ngày,
/// tối đa 5 yêu cầu/giây). API key nằm trong query string nên HttpClient của dịch vụ này tắt log URL (xem DependencyInjection).
/// </summary>
public class GeoapifyPlaceSuggestionService : IPlaceSuggestionService
{
    private readonly HttpClient _http;
    private readonly GeocodingSettings _settings;
    private readonly ILogger<GeoapifyPlaceSuggestionService> _logger;

    public GeoapifyPlaceSuggestionService(HttpClient http, IOptions<GeocodingSettings> settings, ILogger<GeoapifyPlaceSuggestionService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.GeoapifyApiKey);

    public async Task<IReadOnlyList<GeocodeResult>> SuggestAsync(string text, double? biasLatitude, double? biasLongitude, int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) throw new ExternalServiceException("Chưa cấu hình dịch vụ gợi ý địa điểm (Geoapify).");

        var url = $"v1/geocode/autocomplete?format=json&lang=vi&limit={limit}&text={Uri.EscapeDataString(text)}";
        if (!string.IsNullOrWhiteSpace(_settings.CountryCodes))
        {
            url += $"&filter=countrycode:{Uri.EscapeDataString(_settings.CountryCodes.Replace(" ", string.Empty).ToLowerInvariant())}";
        }
        if (biasLatitude is { } lat && biasLongitude is { } lng)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"&bias=proximity:{lng:0.######},{lat:0.######}");
        }
        url += $"&apiKey={Uri.EscapeDataString(_settings.GeoapifyApiKey!)}";

        GeoapifyResponse? body;
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogError("Geoapify từ chối API key (HTTP {Status})", (int)response.StatusCode);
                throw new ExternalServiceException("API key Geoapify không hợp lệ. Kiểm tra biến môi trường Geocoding__GeoapifyApiKey.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ExternalServiceException("Đã dùng hết lượt gợi ý địa điểm miễn phí hôm nay. Hãy dùng nút \"Tìm theo địa chỉ\" hoặc nhập tọa độ.");
            }
            response.EnsureSuccessStatusCode();
            body = await response.Content.ReadFromJsonAsync<GeoapifyResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            _logger.LogWarning("Geoapify không phản hồi khi gợi ý địa điểm: {Error}", ex.GetType().Name);
            throw new ExternalServiceException("Dịch vụ gợi ý địa điểm đang bận. Vui lòng thử lại sau.");
        }

        return (body?.Results ?? new List<GeoapifyPlace>())
            .Where(p => p.Lat is not null && p.Lon is not null && !string.IsNullOrWhiteSpace(p.Formatted))
            .Select(p => new GeocodeResult(p.Formatted!, p.Lat!.Value, p.Lon!.Value, p.ResultType, p.AddressLine1))
            .ToList();
    }

    private sealed class GeoapifyResponse
    {
        [JsonPropertyName("results")] public List<GeoapifyPlace>? Results { get; set; }
    }

    private sealed class GeoapifyPlace
    {
        [JsonPropertyName("formatted")] public string? Formatted { get; set; }
        [JsonPropertyName("address_line1")] public string? AddressLine1 { get; set; }
        [JsonPropertyName("lat")] public double? Lat { get; set; }
        [JsonPropertyName("lon")] public double? Lon { get; set; }
        [JsonPropertyName("result_type")] public string? ResultType { get; set; }
    }
}
