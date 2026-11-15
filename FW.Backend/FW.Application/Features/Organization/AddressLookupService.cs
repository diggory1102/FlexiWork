using System.Text.RegularExpressions;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;

namespace FW.Application.Features.Organization;

public record GeocodeCandidateDto(string DisplayName, double Latitude, double Longitude, string? Category, string? Type);

/// <param name="Query">Địa chỉ đã chuẩn hóa mà người dùng nhập.</param>
/// <param name="MatchedQuery">Phần địa chỉ thực sự tìm thấy (có thể đã bỏ số nhà / tên tòa nhà ở đầu).</param>
/// <param name="Approximate">true nếu phải bỏ bớt phần đầu địa chỉ mới tìm thấy → vị trí gần đúng (cấp phố / phường).</param>
public record AddressLookupDto(string Query, string MatchedQuery, bool Approximate, IReadOnlyList<GeocodeCandidateDto> Candidates);

public interface IAddressLookupService
{
    /// <summary>Tìm tọa độ từ địa chỉ để điền tâm Geofence của chi nhánh.</summary>
    Task<AddressLookupDto> LookupAsync(string? address, CancellationToken cancellationToken = default);

    /// <summary>Gợi ý địa điểm khi đang gõ (Geoapify), ưu tiên kết quả gần vị trí cho trước.</summary>
    Task<IReadOnlyList<GeocodeCandidateDto>> SuggestAsync(string? text, double? biasLatitude, double? biasLongitude,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tra tọa độ từ địa chỉ chi nhánh. Dữ liệu OpenStreetMap ở Việt Nam thường thiếu số nhà / tên tòa nhà, nên nếu không tìm
/// thấy cả địa chỉ thì lần lượt bỏ phần đầu (VD "Tòa nhà FPT, Phố Duy Tân, Cầu Giấy, Hà Nội" → "Phố Duy Tân, Cầu Giấy, Hà Nội")
/// và đánh dấu kết quả là gần đúng. Kết quả được cache để tôn trọng giới hạn 1 yêu cầu/giây của dịch vụ miễn phí.
/// </summary>
public partial class AddressLookupService : IAddressLookupService
{
    public const int MinLength = 5;
    public const int MaxLength = 255;
    public const int MaxCandidates = 5;
    /// <summary>Số lần bỏ phần đầu địa chỉ tối đa (mỗi lần tra mất ~1 giây).</summary>
    public const int MaxFallbacks = 2;
    public const int MinSuggestLength = 3;
    public const int MaxSuggestions = 6;

    private static readonly TimeSpan FoundCacheTime = TimeSpan.FromDays(30);
    private static readonly TimeSpan NotFoundCacheTime = TimeSpan.FromDays(1);
    private static readonly TimeSpan SuggestionCacheTime = TimeSpan.FromDays(7);

    private readonly IGeocodingService _geocoder;
    private readonly IPlaceSuggestionService _suggestions;
    private readonly ICacheService _cache;

    public AddressLookupService(IGeocodingService geocoder, IPlaceSuggestionService suggestions, ICacheService cache)
    {
        _geocoder = geocoder;
        _suggestions = suggestions;
        _cache = cache;
    }

    public async Task<IReadOnlyList<GeocodeCandidateDto>> SuggestAsync(string? text, double? biasLatitude, double? biasLongitude,
        CancellationToken cancellationToken = default)
    {
        var query = Whitespace().Replace(text ?? string.Empty, " ").Trim();
        if (query.Length < MinSuggestLength) return Array.Empty<GeocodeCandidateDto>();
        if (query.Length > MaxLength) query = query[..MaxLength];

        // Vị trí ưu tiên làm tròn ~10 km để các lần gõ cùng khu vực dùng chung cache (tiết kiệm hạn mức miễn phí)
        var hasBias = biasLatitude is >= -90 and <= 90 && biasLongitude is >= -180 and <= 180;
        double? lat = hasBias ? Math.Round(biasLatitude!.Value, 1) : null;
        double? lng = hasBias ? Math.Round(biasLongitude!.Value, 1) : null;

        var key = $"place-suggest:{query.ToLowerInvariant()}|{lat?.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng?.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var cached = await _cache.GetAsync<List<GeocodeCandidateDto>>(key, cancellationToken);
        if (cached != null) return cached;

        if (!_suggestions.IsAvailable)
        {
            throw new ExternalServiceException("Chưa cấu hình gợi ý địa điểm (Geoapify). Hãy dùng nút \"Tìm theo địa chỉ\" hoặc nhập tọa độ.");
        }

        var results = (await _suggestions.SuggestAsync(query, lat, lng, MaxSuggestions, cancellationToken))
            .Select(r => new GeocodeCandidateDto(r.DisplayName, Math.Round(r.Latitude, 7), Math.Round(r.Longitude, 7), r.Category, r.Type))
            .ToList();
        await _cache.SetAsync(key, results, SuggestionCacheTime, cancellationToken);
        return results;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // "Số 25 Lý Thường Kiệt" → "25 Lý Thường Kiệt" (OSM ghi số nhà không kèm chữ "Số")
    [GeneratedRegex(@"^(số|so)\s+(?=\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HouseNumberPrefix();

    public static string Normalize(string? address)
    {
        var text = Whitespace().Replace(address ?? string.Empty, " ").Trim();
        text = HouseNumberPrefix().Replace(text, string.Empty);
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }

    public async Task<AddressLookupDto> LookupAsync(string? address, CancellationToken cancellationToken = default)
    {
        var query = Normalize(address);
        if (query.Length < MinLength) throw new BadRequestException("Vui lòng nhập địa chỉ cụ thể hơn (số nhà, đường, phường, quận, tỉnh/thành).");

        var parts = query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var skip = 0; skip <= MaxFallbacks && skip < parts.Length; skip++)
        {
            // Bỏ phần đầu nhưng luôn giữ ít nhất 2 cấp (tránh chỉ còn "Hà Nội")
            if (skip > 0 && parts.Length - skip < 2) break;

            var candidate = skip == 0 ? query : string.Join(", ", parts.Skip(skip));
            var results = await SearchCachedAsync(candidate, cancellationToken);
            if (results.Count > 0) return new AddressLookupDto(query, candidate, skip > 0, results);
        }

        return new AddressLookupDto(query, query, false, Array.Empty<GeocodeCandidateDto>());
    }

    private async Task<IReadOnlyList<GeocodeCandidateDto>> SearchCachedAsync(string query, CancellationToken cancellationToken)
    {
        var key = $"geocode:{query.ToLowerInvariant()}";
        var cached = await _cache.GetAsync<List<GeocodeCandidateDto>>(key, cancellationToken);
        if (cached != null) return cached;

        var results = (await _geocoder.SearchAsync(query, MaxCandidates, cancellationToken))
            .Select(r => new GeocodeCandidateDto(r.DisplayName, Math.Round(r.Latitude, 7), Math.Round(r.Longitude, 7), r.Category, r.Type))
            .ToList();
        await _cache.SetAsync(key, results, results.Count > 0 ? FoundCacheTime : NotFoundCacheTime, cancellationToken);
        return results;
    }
}
