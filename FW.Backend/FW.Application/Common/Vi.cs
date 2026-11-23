using System.Globalization;

namespace FW.Application.Common;

/// <summary>Tiện ích định dạng ngày/số theo thói quen Việt Nam (không phụ thuộc ICU nên chạy được cả khi InvariantGlobalization).</summary>
public static class Vi
{
    private static readonly string[] WeekdayNames =
        { "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };

    public static string Weekday(DateOnly d) => WeekdayNames[(int)d.DayOfWeek];

    public static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DateWithWeekday(DateOnly d) => $"{Weekday(d)} {Date(d)}";

    public static string Time(TimeSpan t) => $"{t.Hours:00}:{t.Minutes:00}";

    /// <summary>Số thập phân dùng dấu phẩy, bỏ phần thập phân thừa (1,5 / 2 / 0,25).</summary>
    public static string Num(double value, int maxDecimals = 1)
    {
        var s = Math.Round(value, maxDecimals, MidpointRounding.AwayFromZero)
            .ToString("0." + new string('#', maxDecimals), CultureInfo.InvariantCulture);
        return s.Replace('.', ',');
    }

    public static string Num(decimal value, int maxDecimals = 1) => Num((double)value, maxDecimals);

    public static string Percent(double fraction) =>
        $"{Math.Round(fraction * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)}%";
}
