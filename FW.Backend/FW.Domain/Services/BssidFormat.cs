using System.Text.RegularExpressions;

namespace FW.Domain.Services;

/// <summary>
/// Chuẩn hóa địa chỉ MAC Wi-Fi (BSSID) về dạng XX:XX:XX:XX:XX:XX viết hoa — dùng chung cho lúc khai báo Wi-Fi
/// chi nhánh và lúc đối soát khi chấm công, để so khớp chính xác.
/// </summary>
public static partial class BssidFormat
{
    /// <summary>
    /// Android trả về địa chỉ này khi ứng dụng không có quyền vị trí (không đọc được BSSID thật);
    /// 00:00:00:00:00:00 và FF:FF:FF:FF:FF:FF cũng không phải điểm truy cập thật.
    /// </summary>
    private static readonly HashSet<string> Placeholders = new(StringComparer.Ordinal)
    {
        "02:00:00:00:00:00",
        "00:00:00:00:00:00",
        "FF:FF:FF:FF:FF:FF"
    };

    // 6 nhóm, mỗi nhóm 1–2 ký tự hex: iOS bỏ số 0 đứng đầu mỗi nhóm (VD "0:1a:2b:3:4d:5e")
    [GeneratedRegex("^([0-9A-F]{1,2}:){5}[0-9A-F]{1,2}$")]
    private static partial Regex SeparatedPattern();

    [GeneratedRegex("^[0-9A-F]{12}$")]
    private static partial Regex CompactPattern();

    /// <summary>
    /// Thử chuẩn hóa; chấp nhận dấu phân cách ':' hoặc '-' (không trộn lẫn), nhóm 1–2 ký tự hex,
    /// hoặc 12 ký tự hex liền nhau.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var hex = input.Trim().ToUpperInvariant();
        if (hex.Contains(':') && hex.Contains('-')) return false;
        hex = hex.Replace('-', ':');

        if (CompactPattern().IsMatch(hex))
        {
            normalized = string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
            return true;
        }

        if (!SeparatedPattern().IsMatch(hex)) return false;

        normalized = string.Join(":", hex.Split(':').Select(part => part.PadLeft(2, '0')));
        return true;
    }

    /// <summary>Địa chỉ giả (thiết bị không đọc được BSSID thật) — coi như không có thông tin Wi-Fi.</summary>
    public static bool IsPlaceholder(string normalized) => Placeholders.Contains(normalized);
}
