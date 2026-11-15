using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class WifiVerifierTests
{
    private static readonly string[] BranchWifis = { "00:1A:2B:3C:4D:5E", "00:1A:2B:3C:4D:5F" };

    [Theory]
    [InlineData("00:1A:2B:3C:4D:5E")]
    [InlineData("00:1a:2b:3c:4d:5e")] // Android trả chữ thường
    [InlineData("0:1a:2b:3c:4d:5f")]  // iOS bỏ số 0 đầu nhóm
    [InlineData("00-1A-2B-3C-4D-5F")]
    public void Reported_bssid_of_a_branch_router_matches(string reported)
    {
        var result = WifiVerifier.Verify(reported, BranchWifis);
        Assert.Equal(WifiCheckStatus.Matched, result.Status);
        Assert.True(result.IsPassed);
        Assert.StartsWith("00:1A:2B:3C:4D:5", result.Bssid);
    }

    [Fact]
    public void Other_router_does_not_match_but_is_kept_as_evidence()
    {
        var result = WifiVerifier.Verify("aa:bb:cc:dd:ee:ff", BranchWifis);
        Assert.Equal(WifiCheckStatus.NotMatched, result.Status);
        Assert.False(result.IsPassed);
        Assert.Equal("AA:BB:CC:DD:EE:FF", result.Bssid);
    }

    [Fact]
    public void Similar_bssid_of_another_band_does_not_match()
    {
        // So khớp chính xác — BSSID khác một nhóm cuối là điểm truy cập khác
        Assert.Equal(WifiCheckStatus.NotMatched, WifiVerifier.Verify("00:1A:2B:3C:4D:60", BranchWifis).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("không-phải-mac")]
    [InlineData("02:00:00:00:00:00")] // Android không có quyền vị trí
    [InlineData("00:00:00:00:00:00")]
    public void Missing_or_fake_bssid_means_no_wifi_info(string? reported)
    {
        var result = WifiVerifier.Verify(reported, BranchWifis);
        Assert.Equal(WifiCheckStatus.NoWifiInfo, result.Status);
        Assert.False(result.IsPassed);
        Assert.Null(result.Bssid);
    }

    [Fact]
    public void Branch_without_active_wifi_skips_the_layer()
    {
        var result = WifiVerifier.Verify("aa:bb:cc:dd:ee:ff", Array.Empty<string>());
        Assert.Equal(WifiCheckStatus.NotRequired, result.Status);
        Assert.True(result.IsPassed);
        Assert.Equal("AA:BB:CC:DD:EE:FF", result.Bssid); // vẫn lưu làm minh chứng

        Assert.Equal(WifiCheckStatus.NotRequired, WifiVerifier.Verify(null, new[] { "rác" }).Status);
    }
}
