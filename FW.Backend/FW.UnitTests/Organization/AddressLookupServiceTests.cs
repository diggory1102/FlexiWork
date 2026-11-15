using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Organization;
using FW.UnitTests.Fakes;

namespace FW.UnitTests.Organization;

public class AddressLookupServiceTests
{
    /// <summary>Dịch vụ bản đồ giả: chỉ "biết" các địa chỉ khai báo sẵn, ghi lại các truy vấn đã gọi.</summary>
    private sealed class FakeGeocoder : IGeocodingService
    {
        private readonly Dictionary<string, GeocodeResult[]> _known = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Queries { get; } = new();

        public FakeGeocoder Knows(string query, params GeocodeResult[] results)
        {
            _known[query] = results;
            return this;
        }

        public Task<IReadOnlyList<GeocodeResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            IReadOnlyList<GeocodeResult> found = _known.TryGetValue(query, out var r) ? r.Take(limit).ToList() : new List<GeocodeResult>();
            return Task.FromResult(found);
        }
    }

    private static readonly GeocodeResult DuyTan = new("Phố Duy Tân, Dịch Vọng Hậu, Cầu Giấy, Hà Nội", 21.03158412345, 105.78311587654, "highway", "residential");

    private static (AddressLookupService Service, FakeGeocoder Geocoder, FakeClock Clock) Create()
    {
        var clock = new FakeClock();
        var geocoder = new FakeGeocoder();
        return (new AddressLookupService(geocoder, new FakeCache(clock)), geocoder, clock);
    }

    [Theory]
    [InlineData("  Số 25   Lý Thường Kiệt,  Hoàn Kiếm ", "25 Lý Thường Kiệt, Hoàn Kiếm")]
    [InlineData("so 7 Duy Tân", "7 Duy Tân")]
    [InlineData("Số nhà 7 Duy Tân", "Số nhà 7 Duy Tân")] // "Số" không đứng ngay trước chữ số → giữ nguyên
    public void Address_is_normalized(string input, string expected) =>
        Assert.Equal(expected, AddressLookupService.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("HN")]
    public async Task Too_short_address_is_rejected(string? address)
    {
        var (service, geocoder, _) = Create();
        await Assert.ThrowsAsync<BadRequestException>(() => service.LookupAsync(address));
        Assert.Empty(geocoder.Queries);
    }

    [Fact]
    public async Task Exact_match_is_returned_rounded_and_not_approximate()
    {
        var (service, geocoder, _) = Create();
        geocoder.Knows("Phố Duy Tân, Cầu Giấy, Hà Nội", DuyTan);

        var result = await service.LookupAsync("Phố Duy Tân, Cầu Giấy, Hà Nội");
        Assert.False(result.Approximate);
        var c = Assert.Single(result.Candidates);
        Assert.Equal(21.0315841, c.Latitude);
        Assert.Equal(105.7831159, c.Longitude);
        Assert.Equal("highway", c.Category);
    }

    [Fact]
    public async Task Unknown_building_falls_back_to_street_and_is_approximate()
    {
        var (service, geocoder, _) = Create();
        geocoder.Knows("Phố Duy Tân, Dịch Vọng Hậu, Cầu Giấy, Hà Nội", DuyTan);

        var result = await service.LookupAsync("Tòa nhà FPT Cầu Giấy, Phố Duy Tân, Dịch Vọng Hậu, Cầu Giấy, Hà Nội");
        Assert.True(result.Approximate);
        Assert.Equal("Phố Duy Tân, Dịch Vọng Hậu, Cầu Giấy, Hà Nội", result.MatchedQuery);
        Assert.Single(result.Candidates);
        Assert.Equal(2, geocoder.Queries.Count);
    }

    [Fact]
    public async Task Fallback_stops_after_two_drops_and_keeps_at_least_two_levels()
    {
        var (service, geocoder, _) = Create();
        geocoder.Knows("Hà Nội", DuyTan); // chỉ tìm được nếu bỏ tới mức tỉnh/thành — không được phép

        var result = await service.LookupAsync("Ngõ 1, Ngách 2, Hẻm 3, Hà Nội");
        Assert.Empty(result.Candidates);
        Assert.Equal(new[] { "Ngõ 1, Ngách 2, Hẻm 3, Hà Nội", "Ngách 2, Hẻm 3, Hà Nội", "Hẻm 3, Hà Nội" }, geocoder.Queries);

        var (service2, geocoder2, _) = Create();
        await service2.LookupAsync("Duy Tân, Hà Nội");
        Assert.Equal(new[] { "Duy Tân, Hà Nội" }, geocoder2.Queries); // bỏ phần đầu sẽ chỉ còn "Hà Nội"
    }

    [Fact]
    public async Task Results_are_cached_including_misses_until_they_expire()
    {
        var (service, geocoder, clock) = Create();
        geocoder.Knows("Phố Duy Tân, Cầu Giấy, Hà Nội", DuyTan);

        await service.LookupAsync("Phố Duy Tân, Cầu Giấy, Hà Nội");
        await service.LookupAsync("phố duy tân, cầu giấy, hà nội");
        Assert.Single(geocoder.Queries);

        await service.LookupAsync("Không có, ở đâu cả");
        await service.LookupAsync("Không có, ở đâu cả");
        Assert.Equal(2, geocoder.Queries.Count); // kết quả rỗng cũng được cache (1 ngày)

        clock.Advance(TimeSpan.FromDays(2));
        await service.LookupAsync("Không có, ở đâu cả");
        Assert.Equal(3, geocoder.Queries.Count);
    }
}
