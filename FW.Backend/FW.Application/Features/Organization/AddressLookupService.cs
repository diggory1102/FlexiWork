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

    private static readonly TimeSpan FoundCacheTime = TimeSpan.FromDays(30);
    private static readonly TimeSpan NotFoundCacheTime = TimeSpan.FromDays(1);

    private readonly IGeocodingService _geocoder;
    private readonly ICacheService _cache;

    public AddressLookupService(IGeocodingService geocoder, ICacheService cache)
    {
        _geocoder = geocoder;
        _cache = cache;
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
