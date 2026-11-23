using System.Text;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

/// <summary>
/// Quy đổi ngày tương đối ("thứ 5 tuần này") sang ngày cụ thể. Tuần tính từ Thứ Hai đến Chủ nhật (chuẩn Việt Nam).
/// Làm bằng mã thay vì để LLM tự tính vì mô hình nhỏ rất hay sai ngày trong tuần.
/// </summary>
public static class RelativeDateResolver
{
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Thứ Hai = 0
        return date.AddDays(-offset);
    }

    public static DateOnly Resolve(DateOnly today, DayOfWeek day, int weekOffset)
    {
        var monday = StartOfWeek(today).AddDays(7 * weekOffset);
        var index = ((int)day + 6) % 7;
        return monday.AddDays(index);
    }

    /// <summary>Hiểu "thu_5", "thứ 5", "thursday", "5", "chu nhat", "cn"...</summary>
    public static bool TryParseWeekday(string? text, out DayOfWeek day)
    {
        day = default;
        var s = Normalize(text);
        if (s.Length == 0) return false;

        switch (s)
        {
            case "monday" or "mon" or "thu 2" or "thu hai" or "t2" or "2": day = DayOfWeek.Monday; return true;
            case "tuesday" or "tue" or "thu 3" or "thu ba" or "t3" or "3": day = DayOfWeek.Tuesday; return true;
            case "wednesday" or "wed" or "thu 4" or "thu tu" or "t4" or "4": day = DayOfWeek.Wednesday; return true;
            case "thursday" or "thu" or "thu 5" or "thu nam" or "t5" or "5": day = DayOfWeek.Thursday; return true;
            case "friday" or "fri" or "thu 6" or "thu sau" or "t6" or "6": day = DayOfWeek.Friday; return true;
            case "saturday" or "sat" or "thu 7" or "thu bay" or "t7" or "7": day = DayOfWeek.Saturday; return true;
            case "sunday" or "sun" or "chu nhat" or "cn" or "8": day = DayOfWeek.Sunday; return true;
            default: return false;
        }
    }

    /// <summary>this_week → 0, next_week → 1, last_week → -1; chấp nhận cả "tuần này/sau/tới/trước".</summary>
    public static bool TryParseWeek(string? text, out int offset)
    {
        offset = 0;
        var s = Normalize(text);
        if (s.Length == 0) return true; // mặc định tuần này
        switch (s)
        {
            case "this week" or "this" or "current" or "tuan nay": offset = 0; return true;
            case "next week" or "next" or "tuan sau" or "tuan toi" or "tuan keo theo": offset = 1; return true;
            case "last week" or "previous week" or "last" or "tuan truoc" or "tuan qua": offset = -1; return true;
            default: return false;
        }
    }

    /// <summary>Hạ chữ thường, bỏ dấu tiếng Việt, đổi '_' / '-' thành khoảng trắng.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var decomposed = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch is '_' or '-' ? ' ' : ch);
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public enum ShiftPart
{
    Morning,
    Afternoon,
    Evening,
    Night
}

public static class ShiftPartClassifier
{
    /// <summary>Phân loại theo giờ BẮT ĐẦU ca: 05–12h sáng, 12–17h chiều, 17–22h tối, còn lại ca đêm.</summary>
    public static ShiftPart Classify(TimeSpan startTime)
    {
        var h = startTime.TotalHours;
        if (h >= 5 && h < 12) return ShiftPart.Morning;
        if (h >= 12 && h < 17) return ShiftPart.Afternoon;
        if (h >= 17 && h < 22) return ShiftPart.Evening;
        return ShiftPart.Night;
    }

    public static string Label(ShiftPart part) => part switch
    {
        ShiftPart.Morning => "ca sáng",
        ShiftPart.Afternoon => "ca chiều",
        ShiftPart.Evening => "ca tối",
        _ => "ca đêm"
    };

    /// <summary>
    /// "morning|sáng", "afternoon|chiều", "evening|tối", "night|đêm" → phần ca; "all|full_day|cả ngày|rỗng" → null (cả ngày).
    /// Trả false nếu không hiểu.
    /// </summary>
    public static bool TryParse(string? text, out ShiftPart? part)
    {
        part = null;
        var s = RelativeDateResolver.Normalize(text);
        switch (s)
        {
            case "" or "all" or "full day" or "fullday" or "whole day" or "ca ngay" or "ca ngay hom do" or "all day":
                return true;
            case "morning" or "sang" or "ca sang": part = ShiftPart.Morning; return true;
            case "afternoon" or "chieu" or "ca chieu": part = ShiftPart.Afternoon; return true;
            case "evening" or "toi" or "ca toi": part = ShiftPart.Evening; return true;
            case "night" or "dem" or "ca dem": part = ShiftPart.Night; return true;
            default: return false;
        }
    }
}
