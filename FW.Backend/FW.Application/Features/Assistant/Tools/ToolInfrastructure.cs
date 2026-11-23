using System.Globalization;
using System.Text.Json.Nodes;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

/// <summary>
/// Ngữ cảnh một lần gọi công cụ. Danh tính lấy từ JWT của người hỏi, KHÔNG BAO GIỜ từ tham số do mô hình sinh ra —
/// nên dù người dùng "dụ" mô hình truyền user_id khác thì công cụ vẫn chỉ đọc/ghi dữ liệu của chính họ.
/// </summary>
public sealed record ToolContext(
    int UserId,
    int? BranchId,
    IReadOnlyList<string> Roles,
    long? ConversationId,
    DateOnly Today,
    /// <summary>Các tin gần nhất của NGƯỜI DÙNG (mới nhất cuối) — để đối chiếu ngày mô hình điền với ngày người dùng nói.</summary>
    string UserText = "",
    /// <summary>Tin NHẮN HIỆN TẠI của người dùng (không kèm các câu trước).</summary>
    string CurrentText = "")
{
    /// <summary>
    /// Văn bản dùng để neo ngày: ưu tiên câu hiện tại nếu trong đó có nêu ngày (tránh chấp nhận nhầm ngày của các câu hỏi trước,
    /// vd trước đó hỏi "mai em có ca không?" rồi nay xin nghỉ "ngày kia"); câu hiện tại không có ngày thì dùng cả các câu gần đây.
    /// </summary>
    public string DateText => DateMentionExtractor.Extract(CurrentText, Today).Count > 0 ? CurrentText : UserText;
}

public sealed record ToolResult(JsonNode Data, AssistantDraft? Draft = null);

public interface IAssistantTool
{
    string Name { get; }
    string Description { get; }
    JsonObject ParametersSchema { get; }

    /// <summary>Vai trò được dùng công cụ; null = mọi người đã đăng nhập. Công cụ không được phép sẽ không được nạp cho mô hình.</summary>
    IReadOnlyCollection<string>? AllowedRoles { get; }

    Task<ToolResult> ExecuteAsync(ToolContext context, JsonObject args, CancellationToken ct);
}

/// <summary>Tham số sai — thông báo được gửi lại cho mô hình để nó tự sửa và gọi lại.</summary>
public sealed class ToolArgumentException : Exception
{
    public ToolArgumentException(string message) : base(message) { }
}

/// <summary>Đọc tham số "dễ tính" (mô hình nhỏ hay trả số dạng chuỗi, ngày dạng dd/MM/yyyy...).</summary>
public static class ToolArgs
{
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" };

    public static string? Str(JsonObject args, string name)
    {
        if (!args.TryGetPropertyValue(name, out var node) || node is null) return null;
        var s = node is JsonValue v && v.TryGetValue<string>(out var str) ? str : node.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public static int? Int(JsonObject args, string name)
    {
        var s = Str(args, name);
        if (s is null) return null;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == Math.Floor(d)) return (int)d;
        throw new ToolArgumentException($"'{name}' phải là số nguyên, nhận được \"{s}\".");
    }

    public static DateOnly? Date(JsonObject args, string name)
    {
        var s = Str(args, name);
        if (s is null) return null;
        if (DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        throw new ToolArgumentException($"'{name}' phải có dạng yyyy-MM-dd (VD 2026-11-19), nhận được \"{s}\".");
    }

    private static readonly string[] TimeFormats = { "HH:mm", "H:mm", "HH:mm:ss", "H'h'mm", "H'h'", "HH'h'mm", "HH'h'" };

    /// <summary>Giờ trong ngày: 18:00, 18h, 18h30.</summary>
    public static TimeOnly? Time(JsonObject args, string name)
    {
        var s = Str(args, name);
        if (s is null) return null;
        if (TimeOnly.TryParseExact(s.ToLowerInvariant(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t;
        throw new ToolArgumentException($"'{name}' phải có dạng HH:mm (VD 18:00), nhận được \"{s}\".");
    }

    public static ShiftPart? Part(JsonObject args, string name)
    {
        var s = Str(args, name);
        if (!ShiftPartClassifier.TryParse(s, out var part))
            throw new ToolArgumentException($"'{name}' chỉ nhận morning | afternoon | evening | night | full_day, nhận được \"{s}\".");
        return part;
    }

    /// <summary>
    /// Ngày được chỉ định bằng ISO (<paramref name="dateKey"/>) hoặc bằng thứ + tuần (<c>weekday</c> + <c>week</c>).
    /// Trả null nếu không có tham số nào.
    /// </summary>
    public static DateOnly? DateOrWeekday(JsonObject args, string dateKey, DateOnly today, string weekdayKey = "weekday", string weekKey = "week")
    {
        var iso = Date(args, dateKey);
        if (iso is not null) return iso;

        // "hôm nay / ngày mai / ngày kia" — mã tính, mô hình nhỏ hay lệch một ngày khi tự cộng
        var rel = Str(args, "relative_day");
        if (rel is not null)
        {
            return RelativeDateResolver.Normalize(rel) switch
            {
                "today" or "hom nay" => today,
                "tomorrow" or "ngay mai" or "mai" => today.AddDays(1),
                "day after tomorrow" or "ngay kia" or "kia" => today.AddDays(2),
                "yesterday" or "hom qua" => today.AddDays(-1),
                _ => throw new ToolArgumentException(
                    $"'relative_day' chỉ nhận today | tomorrow | day_after_tomorrow | yesterday, nhận được \"{rel}\".")
            };
        }

        var wd = Str(args, weekdayKey);
        if (wd is null) return null;
        if (!RelativeDateResolver.TryParseWeekday(wd, out var day))
            throw new ToolArgumentException($"'{weekdayKey}' không hiểu: \"{wd}\". Dùng monday..sunday hoặc truyền {dateKey}.");
        if (!RelativeDateResolver.TryParseWeek(Str(args, weekKey), out var offset))
            throw new ToolArgumentException($"'{weekKey}' chỉ nhận this_week | next_week | last_week.");
        return RelativeDateResolver.Resolve(today, day, offset);
    }
}

/// <summary>Dựng JSON Schema ngắn gọn cho khai báo công cụ.</summary>
public static class Schema
{
    public static JsonObject Object(JsonObject properties, params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0) schema["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        return schema;
    }

    public static JsonObject Str(string description, params string[] enumValues)
    {
        var o = new JsonObject { ["type"] = "string", ["description"] = description };
        if (enumValues.Length > 0) o["enum"] = new JsonArray(enumValues.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());
        return o;
    }

    public static JsonObject Int(string description) => new() { ["type"] = "integer", ["description"] = description };

    public static JsonObject Empty() => Object(new JsonObject());
}
