namespace FW.Domain.Services;

/// <summary>Kết quả kiểm tra lớp 2 (Wi-Fi BSSID).</summary>
public enum WifiCheckStatus
{
    /// <summary>Điện thoại đang bắt Wi-Fi nội bộ đã khai báo của chi nhánh.</summary>
    Matched,

    /// <summary>Đang kết nối một Wi-Fi khác (không thuộc danh sách của chi nhánh).</summary>
    NotMatched,

    /// <summary>Không gửi BSSID, BSSID sai định dạng hoặc địa chỉ giả (không bật Wi-Fi, thiếu quyền vị trí...).</summary>
    NoWifiInfo,

    /// <summary>Chi nhánh chưa khai báo Wi-Fi nào đang hoạt động → bỏ qua lớp này.</summary>
    NotRequired
}

/// <param name="Bssid">BSSID đã chuẩn hóa (null nếu không có / không hợp lệ) — lưu làm minh chứng.</param>
public record WifiCheckResult(WifiCheckStatus Status, string? Bssid)
{
    /// <summary>Lớp 2 đạt: khớp Wi-Fi nội bộ hoặc chi nhánh không yêu cầu Wi-Fi.</summary>
    public bool IsPassed => Status is WifiCheckStatus.Matched or WifiCheckStatus.NotRequired;
}

/// <summary>
/// Lớp 2 của chấm công: đối soát BSSID (địa chỉ MAC của router) mà điện thoại đang kết nối với danh sách
/// Wi-Fi nội bộ của chi nhánh. Phần mềm giả lập vị trí (Fake GPS) đổi được tọa độ nhưng không đổi được router
/// mà điện thoại bắt được, nên lớp này bổ sung cho lớp GPS. So khớp chính xác cả 6 nhóm — router hai băng tần
/// (2,4 GHz / 5 GHz) có BSSID khác nhau, cần khai báo đủ từng BSSID.
/// </summary>
public static class WifiVerifier
{
    /// <param name="reportedBssid">BSSID điện thoại gửi lên (định dạng bất kỳ mà <see cref="BssidFormat"/> chấp nhận).</param>
    /// <param name="branchBssids">BSSID các Wi-Fi đang hoạt động của chi nhánh (đã chuẩn hóa khi khai báo).</param>
    public static WifiCheckResult Verify(string? reportedBssid, IEnumerable<string> branchBssids)
    {
        var allowed = branchBssids
            .Select(b => BssidFormat.TryNormalize(b, out var n) ? n : null)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var bssid = BssidFormat.TryNormalize(reportedBssid, out var normalized) && !BssidFormat.IsPlaceholder(normalized)
            ? normalized
            : null;

        if (allowed.Count == 0) return new WifiCheckResult(WifiCheckStatus.NotRequired, bssid);
        if (bssid is null) return new WifiCheckResult(WifiCheckStatus.NoWifiInfo, null);

        return new WifiCheckResult(allowed.Contains(bssid) ? WifiCheckStatus.Matched : WifiCheckStatus.NotMatched, bssid);
    }
}
