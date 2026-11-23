using System.Globalization;
using System.Text.RegularExpressions;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

/// <summary>
/// Rút các NGÀY mà người dùng nói trong câu ("ngày mai", "thứ 5 tuần này", "25/11", "ngày 20"...) bằng mã.
/// Dùng để đối chiếu ngày do mô hình điền: mô hình nhỏ rất hay điền sai ngày ISO (không biết cộng ngày)
/// dù người dùng nói rất rõ. Hiểu thiếu còn hơn hiểu sai: chỉ cần ít mẫu nhưng chắc chắn, không tìm ra gì thì không can thiệp.
/// </summary>
public static partial class DateMentionExtractor
{
    public static IReadOnlySet<DateOnly> Extract(string? text, DateOnly today)
    {
        var set = new HashSet<DateOnly>();
        var s = RelativeDateResolver.Normalize(text);
        if (s.Length == 0) return set;

        // Từ chỉ ngày tương đối. "mai" đứng riêng cũng là ngày mai (sáng mai, chiều mai, mai em...)
        if (Regex.IsMatch(s, @"\bhom nay\b")) set.Add(today);
        if (Regex.IsMatch(s, @"\b(ngay )?mai\b")) set.Add(today.AddDays(1));
        if (Regex.IsMatch(s, @"\b(ngay )?kia\b")) set.Add(today.AddDays(2));
        if (Regex.IsMatch(s, @"\bhom qua\b")) set.Add(today.AddDays(-1));

        // "thứ 5 và thứ 6 tuần này": nếu cả câu chỉ có MỘT cách nói tuần thì áp cho mọi thứ chưa kèm tuần
        var weekPhrases = WeekPhraseRegex().Matches(s).Select(x => x.Value).Distinct().ToList();
        var sharedWeek = weekPhrases.Count == 1 ? weekPhrases[0] : string.Empty;

        // "thứ 5", "thứ năm", "chủ nhật" + tuần này / sau / trước (tùy chọn)
        foreach (Match m in WeekdayRegex().Matches(s))
        {
            var name = m.Groups["day"].Value;
            if (!RelativeDateResolver.TryParseWeekday(name == "chu nhat" ? "chu nhat" : "thu " + name, out var day)) continue;

            var week = m.Groups["week"].Value.Length > 0 ? m.Groups["week"].Value : sharedWeek;
            if (week.Length == 0)
            {
                // Không nói tuần: chấp nhận cả hai lần xuất hiện sắp tới (kể cả hôm nay nếu trùng thứ)
                var first = RelativeDateResolver.Resolve(today, day, 0);
                if (first < today) first = first.AddDays(7);
                set.Add(first);
                set.Add(first.AddDays(7));
            }
            else
            {
                RelativeDateResolver.TryParseWeek(week, out var offset);
                set.Add(RelativeDateResolver.Resolve(today, day, offset));
            }
        }

        // dd/MM[/yyyy]
        foreach (Match m in SlashDateRegex().Matches(s))
            TryAdd(set, m.Groups["d"].Value, m.Groups["m"].Value, m.Groups["y"].Value, today);

        // "ngày 25 tháng 11"
        foreach (Match m in DayMonthWordsRegex().Matches(s))
            TryAdd(set, m.Groups["d"].Value, m.Groups["m"].Value, string.Empty, today);

        // "ngày 25" (chỉ có ngày): tháng này, hoặc tháng sau nếu đã qua
        foreach (Match m in DayOnlyRegex().Matches(s))
        {
            if (!int.TryParse(m.Groups["d"].Value, out var d) || d is < 1 or > 31) continue;
            foreach (var monthOffset in new[] { 0, 1 })
            {
                var first = new DateOnly(today.Year, today.Month, 1).AddMonths(monthOffset);
                if (d <= DateTime.DaysInMonth(first.Year, first.Month) && new DateOnly(first.Year, first.Month, d) is var c && c >= today.AddDays(-1))
                    set.Add(c);
            }
        }

        return set;
    }

    private static void TryAdd(HashSet<DateOnly> set, string d, string m, string y, DateOnly today)
    {
        if (!int.TryParse(d, out var day) || !int.TryParse(m, out var month)) return;
        var year = today.Year;
        if (y.Length > 0 && int.TryParse(y, out var yy)) year = yy < 100 ? 2000 + yy : yy;
        if (month is < 1 or > 12 || year is < 2000 or > 2100) return;
        if (day < 1 || day > DateTime.DaysInMonth(year, month)) return;
        set.Add(new DateOnly(year, month, day));
    }

    [GeneratedRegex(@"\b(?:thu (?<day>2|3|4|5|6|7|hai|ba|tu|nam|sau|bay)|(?<day>chu nhat)|\bcn\b)(?: (?<week>tuan nay|tuan sau|tuan toi|tuan truoc|tuan qua))?")]
    private static partial Regex WeekdayRegex();

    [GeneratedRegex(@"tuan (?:nay|sau|toi|truoc|qua)")]
    private static partial Regex WeekPhraseRegex();

    [GeneratedRegex(@"\b(?<d>\d{1,2})\s*/\s*(?<m>\d{1,2})(?:\s*/\s*(?<y>\d{2,4}))?\b")]
    private static partial Regex SlashDateRegex();

    [GeneratedRegex(@"\bngay (?<d>\d{1,2}) thang (?<m>\d{1,2})\b")]
    private static partial Regex DayMonthWordsRegex();

    [GeneratedRegex(@"\bngay (?<d>\d{1,2})\b(?! thang)(?! ?/)")]
    private static partial Regex DayOnlyRegex();
}

/// <summary>Kết quả đối chiếu ngày: ngày cuối cùng dùng và ghi chú (nếu đã tự sửa).</summary>
public sealed record GroundedRange(DateOnly Start, DateOnly End, string? Note);

public static class DateGrounding
{
    /// <summary>
    /// Đối chiếu khoảng ngày do mô hình điền với ngày người dùng nói.
    /// • Người dùng không nói ngày nào → giữ nguyên (ngày có thể đến từ lượt hội thoại trước).
    /// • Ngày mô hình điền nằm trong số ngày người dùng nói → giữ nguyên.
    /// • Người dùng chỉ nói ĐÚNG MỘT ngày mà mô hình điền khác → tự sửa theo lời người dùng và ghi chú.
    /// • Người dùng nói nhiều ngày mà mô hình điền ngoài đó → ném lỗi kèm danh sách ngày hợp lệ để mô hình gọi lại.
    /// </summary>
    public static GroundedRange Apply(DateOnly start, DateOnly end, string? userText, DateOnly today)
    {
        var mentions = DateMentionExtractor.Extract(userText, today);
        if (mentions.Count == 0) return new GroundedRange(start, end, null);

        var startOk = mentions.Contains(start);
        var endOk = end == start ? startOk : mentions.Contains(end);
        if (startOk && endOk) return new GroundedRange(start, end, null);

        if (mentions.Count == 1)
        {
            var only = mentions.Single();
            return new GroundedRange(only, only,
                $"Đã dùng ngày {Vi.DateWithWeekday(only)} theo đúng lời người dùng (ngày bạn điền là {Vi.Date(start)}).");
        }

        var allowed = string.Join(", ", mentions.OrderBy(d => d).Select(d => $"{d:yyyy-MM-dd} ({Vi.Weekday(d)})"));
        throw new ToolArgumentException(
            $"Ngày bạn điền ({start:yyyy-MM-dd}{(end != start ? " → " + end.ToString("yyyy-MM-dd") : string.Empty)}) không khớp ngày người dùng nói. " +
            $"Người dùng nhắc tới các ngày: {allowed}. Hãy gọi lại với ngày lấy từ danh sách này (đừng tự tính ngày)." +
            (mentions.Count >= 2
                ? $" Nếu nghỉ liên tiếp: start_date={mentions.Min():yyyy-MM-dd}, end_date={mentions.Max():yyyy-MM-dd}."
                : string.Empty));
    }

    /// <summary>
    /// Mô hình quên truyền ngày dù người dùng đã nói ("tối mai", "hôm qua"): nếu câu chỉ nhắc ĐÚNG MỘT ngày thì dùng ngày đó,
    /// ngược lại trả null để công cụ báo thiếu ngày và mô hình hỏi lại.
    /// </summary>
    public static DateOnly? OnlyMentionedDate(string? userText, DateOnly today)
    {
        var mentions = DateMentionExtractor.Extract(userText, today);
        return mentions.Count == 1 ? mentions.Single() : null;
    }

    /// <summary>
    /// Tên đồng nghiệp do mô hình điền phải xuất hiện trong lời người dùng (ít nhất một từ trong tên).
    /// Chặn trường hợp mô hình tự bịa tên khi người dùng chưa hề nói muốn đổi ca với ai.
    /// </summary>
    public static bool NameWasMentioned(string? name, string? userText)
    {
        static string Words(string? s) => Regex.Replace(RelativeDateResolver.Normalize(s), "[^a-z0-9]+", " ").Trim();
        var text = " " + Words(userText) + " ";
        return Words(name)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Length >= 2 && text.Contains(" " + token + " "));
    }

    // Từ đệm không mang nghĩa lý do — bỏ qua khi so khớp lý do với lời người dùng
    private static readonly HashSet<string> ReasonStopWords = new()
    {
        "vi", "do", "de", "bi", "la", "em", "minh", "toi", "co", "cua", "va", "nen", "can", "phai", "muon", "xin", "nghi", "viec"
    };

    /// <summary>
    /// Lý do mô hình điền phải bám lời người dùng (ít nhất một từ có nghĩa xuất hiện trong câu). Chặn việc mô hình tự bịa lý do
    /// ("bận thi", "kiểm kho"...) khi người dùng chưa nói; nói lại bằng từ khác cùng nghĩa thì mô hình gọi lại với đúng lời họ.
    /// </summary>
    public static bool ReasonWasMentioned(string? reason, string? userText)
    {
        static string Words(string? s) => Regex.Replace(RelativeDateResolver.Normalize(s), "[^a-z0-9]+", " ").Trim();
        var text = " " + Words(userText) + " ";
        var tokens = Words(reason).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !ReasonStopWords.Contains(t)).ToList();
        // Lý do chỉ gồm từ đệm ("bận việc", "có việc"): so cả từ đệm
        if (tokens.Count == 0) tokens = Words(reason).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        return tokens.Any(t => text.Contains(" " + t + " "));
    }

    public const string ReasonNotSaidMessage =
        "Lý do bạn điền không có trong lời người dùng. Chỉ dùng đúng lý do người dùng đã nói; nếu họ chưa nêu lý do thì hãy hỏi lại họ, đừng tự bịa.";

    public const string DateNotSaidMessage =
        "Người dùng chưa nói ngày nào. Đừng tự chọn ngày: hãy hỏi người dùng muốn ngày nào.";

    /// <summary>Chặn bản nháp khi cả cuộc trò chuyện gần đây chưa hề có ngày nào (ngày do mô hình tự chọn) hoặc lý do do mô hình tự bịa.</summary>
    public static void RequireUserSaidDateAndReason(string? reason, string? userText, DateOnly today)
    {
        if (DateMentionExtractor.Extract(userText, today).Count == 0)
            throw new ToolArgumentException(DateNotSaidMessage);
        if (!string.IsNullOrWhiteSpace(reason) && !ReasonWasMentioned(reason, userText))
            throw new ToolArgumentException(ReasonNotSaidMessage);
    }

    /// <summary>Đối chiếu một ngày đơn (ví dụ ngày ca bên đồng nghiệp).</summary>
    public static DateOnly ApplySingle(DateOnly date, string? userText, DateOnly today) =>
        Apply(date, date, userText, today).Start;
}

/// <summary>
/// Neo CA (sáng/chiều/tối/đêm) theo lời người dùng — cùng tinh thần với <see cref="DateGrounding"/>:
/// mô hình nhỏ hay bỏ sót "ca chiều" (thành nghỉ cả ngày) hoặc điền nhầm buổi.
/// Chỉ can thiệp khi người dùng nhắc ĐÚNG MỘT buổi; nhắc nhiều buổi hoặc không nhắc thì giữ nguyên lựa chọn của mô hình.
/// </summary>
public static partial class ShiftPartGrounding
{
    public static ShiftPart? OnlyMention(string? text)
    {
        var s = RelativeDateResolver.Normalize(text);
        if (s.Length == 0) return null;

        var found = new HashSet<ShiftPart>();
        foreach (Match m in PartRegex().Matches(s))
        {
            found.Add(m.Groups["p"].Value switch
            {
                "sang" => ShiftPart.Morning,
                "chieu" => ShiftPart.Afternoon,
                "toi" => ShiftPart.Evening,
                _ => ShiftPart.Night
            });
        }
        return found.Count == 1 ? found.Single() : null;
    }

    /// <summary>Trả buổi cuối cùng dùng và ghi chú nếu đã sửa.</summary>
    public static (ShiftPart? Part, string? Note) Apply(ShiftPart? modelPart, string? userText)
    {
        var said = OnlyMention(userText);
        if (said is null || said == modelPart) return (modelPart, null);
        return (said, $"Đã dùng {ShiftPartClassifier.Label(said.Value)} theo đúng lời người dùng.");
    }

    // "ca sáng", "ca chiều", "buổi tối", "ca đêm"
    [GeneratedRegex(@"\b(?:ca|buoi) (?<p>sang|chieu|toi|dem)\b")]
    private static partial Regex PartRegex();
}
