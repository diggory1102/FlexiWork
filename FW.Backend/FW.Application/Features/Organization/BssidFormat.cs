using System.Text.RegularExpressions;

namespace FW.Application.Features.Organization;

/// <summary>
/// Chuẩn hóa địa chỉ MAC Wi-Fi (BSSID) về dạng XX:XX:XX:XX:XX:XX viết hoa — cùng định dạng mà
/// ứng dụng di động gửi lên khi chấm công, để so khớp chính xác.
/// </summary>
public static partial class BssidFormat
{
    [GeneratedRegex("^([0-9A-F]{2}:){5}[0-9A-F]{2}$")]
    private static partial Regex CanonicalPattern();

    /// <summary>Thử chuẩn hóa; chấp nhận dấu phân cách ':' hoặc '-', hoặc 12 ký tự hex liền nhau.</summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var hex = input.Trim().ToUpperInvariant().Replace("-", ":");
        if (!hex.Contains(':') && hex.Length == 12)
        {
            hex = string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        }

        if (!CanonicalPattern().IsMatch(hex)) return false;

        normalized = hex;
        return true;
    }
}
