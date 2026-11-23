using System.Text.Json.Nodes;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

internal static class DraftToolResults
{
    public static ToolResult FromBuild(DraftBuildResult r)
    {
        if (!r.Ok)
        {
            var fail = new JsonObject { ["ok"] = false, ["error_code"] = r.Code, ["message"] = r.Message };
            if (r.Details is not null) fail["details"] = r.Details.DeepClone();
            return new ToolResult(fail);
        }

        var draft = r.Draft!;
        var data = new JsonObject
        {
            ["ok"] = true,
            ["draft_id"] = draft.Id.ToString(),
            ["status"] = "draft_created_not_sent",
            ["message"] =
                "Đã tạo BẢN NHÁP, CHƯA gửi. Hãy tóm tắt bản nháp cho người dùng, nhắc cảnh báo (nếu có) và bảo họ bấm nút Xác nhận để gửi. Không được nói là đã gửi đơn.",
            ["summary"] = new JsonArray(draft.Card.Lines.Select(l => (JsonNode)new JsonObject
            {
                ["label"] = l.Label,
                ["value"] = l.Value
            }).ToArray()),
            ["warnings"] = new JsonArray(draft.Card.Warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray()),
            ["requires_attachment"] = draft.Card.RequiresAttachment
        };
        return new ToolResult(data, draft);
    }
}

/// <summary>Tạo bản nháp đơn xin nghỉ từ câu nói tự nhiên.</summary>
public sealed class DraftLeaveRequestTool : IAssistantTool
{
    private readonly DraftService _drafts;

    public DraftLeaveRequestTool(DraftService drafts) => _drafts = drafts;

    public string Name => "draft_leave_request";

    public string Description =>
        "Tạo BẢN NHÁP đơn xin nghỉ cho người đang hỏi (chưa gửi — người dùng phải bấm xác nhận). " +
        "Dùng khi người dùng muốn xin nghỉ, vd 'cho em xin nghỉ ca sáng thứ 5 tuần này vì bận thi'. " +
        "Ngày: truyền start_date (yyyy-MM-dd) HOẶC relative_day (hôm nay/ngày mai/ngày kia) HOẶC weekday + week (hệ thống tự quy đổi, KHÔNG tự tính ngày). " +
        "Phải có lý do — nếu người dùng chưa nói thì hỏi lại, đừng bịa.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["leave_type_id"] = Schema.Int("Mã loại nghỉ trong danh sách hướng dẫn hệ thống. Bỏ trống = nghỉ phép năm."),
        ["start_date"] = Schema.Str("Ngày nghỉ (hoặc ngày đầu nếu nghỉ nhiều ngày), yyyy-MM-dd."),
        ["end_date"] = Schema.Str("Ngày cuối nếu nghỉ nhiều ngày, yyyy-MM-dd. Bỏ trống = nghỉ đúng một ngày."),
        ["relative_day"] = Schema.Str("Thay cho start_date khi người dùng nói hôm nay / ngày mai / ngày kia (hệ thống tự quy đổi).",
            "today", "tomorrow", "day_after_tomorrow"),
        ["weekday"] = Schema.Str("Thay cho start_date khi người dùng nói 'thứ 5 tuần này'.",
            "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"),
        ["week"] = Schema.Str("Đi kèm weekday. Mặc định this_week.", "this_week", "next_week"),
        ["shift_part"] = Schema.Str("Nghỉ ca nào trong ngày. Bỏ trống hoặc full_day = nghỉ cả ngày.",
            "morning", "afternoon", "evening", "night", "full_day"),
        ["reason"] = Schema.Str("Lý do nghỉ do người dùng nói, vd 'bận thi'.")
    }, "reason");

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var typeId = ToolArgs.Int(args, "leave_type_id");
        var start = ToolArgs.DateOrWeekday(args, "start_date", ctx.Today)
                    ?? throw new ToolArgumentException("Thiếu ngày nghỉ: truyền start_date, relative_day hoặc weekday (+ week).");
        var end = ToolArgs.Date(args, "end_date") ?? start;
        var (part, partNote) = ShiftPartGrounding.Apply(ToolArgs.Part(args, "shift_part"), ctx.UserText);
        var reason = ToolArgs.Str(args, "reason") ?? string.Empty;
        DateGrounding.RequireUserSaidDateAndReason(reason, ctx.UserText, ctx.Today);

        // Neo ngày theo lời người dùng: mô hình nhỏ hay điền sai ngày ISO
        var grounded = DateGrounding.Apply(start, end, ctx.UserText, ctx.Today);

        var result = await _drafts.BuildLeaveDraftAsync(
            ctx.UserId, new LeaveDraftRequest(typeId, grounded.Start, grounded.End, part, reason), ctx.ConversationId, ct);
        var tool = DraftToolResults.FromBuild(result);
        if (grounded.Note is not null && tool.Data is JsonObject o) o["date_note"] = grounded.Note;
        if (partNote is not null && tool.Data is JsonObject p) p["shift_note"] = partNote;
        return tool;
    }
}

/// <summary>Tạo bản nháp đơn xin đổi ca với đồng nghiệp.</summary>
public sealed class DraftShiftSwapTool : IAssistantTool
{
    private readonly DraftService _drafts;

    public DraftShiftSwapTool(DraftService drafts) => _drafts = drafts;

    public string Name => "draft_shift_swap_request";

    public string Description =>
        "Tạo BẢN NHÁP đơn xin đổi ca với một đồng nghiệp cùng chi nhánh (chưa gửi — người dùng phải bấm xác nhận). " +
        "Dùng khi người dùng muốn đổi/nhường ca, vd 'em muốn đổi ca chiều thứ 6 với Lan'. Phải có tên đồng nghiệp và lý do.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["date"] = Schema.Str("Ngày của ca muốn đổi, yyyy-MM-dd."),
        ["relative_day"] = Schema.Str("Thay cho date khi người dùng nói hôm nay / ngày mai / ngày kia (hệ thống tự quy đổi).",
            "today", "tomorrow", "day_after_tomorrow"),
        ["weekday"] = Schema.Str("Thay cho date khi người dùng nói 'thứ 6 tuần này'.",
            "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"),
        ["week"] = Schema.Str("Đi kèm weekday. Mặc định this_week.", "this_week", "next_week"),
        ["shift_part"] = Schema.Str("Ca của người dùng muốn đổi.", "morning", "afternoon", "evening", "night"),
        ["colleague_name"] = Schema.Str("Tên đồng nghiệp muốn đổi ca cùng."),
        ["target_date"] = Schema.Str("Ngày của ca bên đồng nghiệp, yyyy-MM-dd. Bỏ trống = cùng ngày."),
        ["target_shift_part"] = Schema.Str("Ca bên đồng nghiệp muốn nhận.", "morning", "afternoon", "evening", "night"),
        ["reason"] = Schema.Str("Lý do đổi ca.")
    }, "colleague_name", "reason");

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var date = ToolArgs.DateOrWeekday(args, "date", ctx.Today)
                   ?? DateGrounding.OnlyMentionedDate(ctx.UserText, ctx.Today)
                   ?? throw new ToolArgumentException("Thiếu ngày của ca muốn đổi: truyền date, relative_day hoặc weekday (+ week).");
        var colleague = ToolArgs.Str(args, "colleague_name") ?? string.Empty;
        if (!DateGrounding.NameWasMentioned(colleague, ctx.UserText))
            throw new ToolArgumentException("Người dùng chưa nói muốn đổi ca với đồng nghiệp nào. Đừng tự đặt tên: hãy hỏi người dùng tên đồng nghiệp.");
        DateGrounding.RequireUserSaidDateAndReason(ToolArgs.Str(args, "reason"), ctx.UserText, ctx.Today);
        var grounded = DateGrounding.Apply(date, date, ctx.UserText, ctx.Today);
        var (shiftPart, partNote) = ShiftPartGrounding.Apply(ToolArgs.Part(args, "shift_part"), ctx.UserText);
        var targetDate = ToolArgs.Date(args, "target_date");
        // Ngày bên đồng nghiệp: nếu trùng ngày gốc (đã bị sửa) thì để trống = cùng ngày
        if (targetDate is { } td && td == date) targetDate = null;

        var result = await _drafts.BuildSwapDraftAsync(
            ctx.UserId,
            new SwapDraftRequest(
                grounded.Start,
                shiftPart,
                colleague,
                targetDate,
                ToolArgs.Part(args, "target_shift_part"),
                ToolArgs.Str(args, "reason") ?? string.Empty),
            ctx.ConversationId, ct);
        var tool = DraftToolResults.FromBuild(result);
        if (grounded.Note is not null && tool.Data is JsonObject o) o["date_note"] = grounded.Note;
        if (partNote is not null && tool.Data is JsonObject p) p["shift_note"] = partNote;
        return tool;
    }
}


/// <summary>Tạo bản nháp đơn làm thêm giờ (OT).</summary>
public sealed class DraftOvertimeRequestTool : IAssistantTool
{
    private readonly DraftService _drafts;

    public DraftOvertimeRequestTool(DraftService drafts) => _drafts = drafts;

    public string Name => "draft_overtime_request";

    public string Description =>
        "Tạo BẢN NHÁP đơn đăng ký làm thêm giờ (OT) cho người đang hỏi (chưa gửi — người dùng phải bấm xác nhận). " +
        "Dùng khi người dùng muốn đăng ký làm thêm, vd 'đăng ký OT tối mai từ 18h đến 21h để kiểm kho'. " +
        "Phải có ngày, giờ bắt đầu, giờ kết thúc và lý do — thiếu thì hỏi lại, đừng bịa. Giờ kết thúc nhỏ hơn giờ bắt đầu nghĩa là qua nửa đêm.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["date"] = Schema.Str("Ngày bắt đầu làm thêm, yyyy-MM-dd."),
        ["relative_day"] = Schema.Str("Thay cho date khi người dùng nói hôm nay / ngày mai / ngày kia (hệ thống tự quy đổi).",
            "today", "tomorrow", "day_after_tomorrow"),
        ["weekday"] = Schema.Str("Thay cho date khi người dùng nói 'thứ 7 tuần này'.",
            "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"),
        ["week"] = Schema.Str("Đi kèm weekday. Mặc định this_week.", "this_week", "next_week"),
        ["start_time"] = Schema.Str("Giờ bắt đầu OT, dạng HH:mm (24 giờ), vd 18:00."),
        ["end_time"] = Schema.Str("Giờ kết thúc OT, dạng HH:mm (24 giờ), vd 21:30."),
        ["reason"] = Schema.Str("Lý do làm thêm giờ do người dùng nói.")
    }, "start_time", "end_time", "reason");

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var date = ToolArgs.DateOrWeekday(args, "date", ctx.Today)
                   ?? DateGrounding.OnlyMentionedDate(ctx.UserText, ctx.Today)
                   ?? throw new ToolArgumentException("Thiếu ngày làm thêm: truyền date, relative_day hoặc weekday (+ week).");
        DateGrounding.RequireUserSaidDateAndReason(ToolArgs.Str(args, "reason"), ctx.UserText, ctx.Today);
        var grounded = DateGrounding.Apply(date, date, ctx.UserText, ctx.Today);
        var start = ToolArgs.Time(args, "start_time") ?? throw new ToolArgumentException("Thiếu giờ bắt đầu OT (start_time, dạng HH:mm).");
        var end = ToolArgs.Time(args, "end_time") ?? throw new ToolArgumentException("Thiếu giờ kết thúc OT (end_time, dạng HH:mm).");

        var result = await _drafts.BuildOvertimeDraftAsync(
            ctx.UserId, new OvertimeDraftRequest(grounded.Start, start, end, ToolArgs.Str(args, "reason") ?? string.Empty), ctx.ConversationId, ct);
        var tool = DraftToolResults.FromBuild(result);
        if (grounded.Note is not null && tool.Data is JsonObject o) o["date_note"] = grounded.Note;
        return tool;
    }
}

/// <summary>Tạo bản nháp đơn giải trình đi muộn / về sớm / lượt chấm công chờ duyệt.</summary>
public sealed class DraftExplanationRequestTool : IAssistantTool
{
    private readonly DraftService _drafts;

    public DraftExplanationRequestTool(DraftService drafts) => _drafts = drafts;

    public string Name => "draft_explanation_request";

    public string Description =>
        "Tạo BẢN NHÁP đơn giải trình cho một lượt chấm công bị đi muộn, về sớm hoặc đang chờ duyệt (chưa gửi — người dùng phải bấm xác nhận). " +
        "Dùng khi người dùng muốn giải trình, vd 'hôm qua em đi muộn vì kẹt xe, làm đơn giải trình giúp em'. " +
        "Chỉ giải trình được lượt chấm công trong 7 ngày gần đây. Phải có ngày của ca và lý do — thiếu thì hỏi lại.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["date"] = Schema.Str("Ngày công của ca cần giải trình, yyyy-MM-dd."),
        ["relative_day"] = Schema.Str("Thay cho date khi người dùng nói hôm nay / hôm qua (hệ thống tự quy đổi).",
            "today", "yesterday"),
        ["weekday"] = Schema.Str("Thay cho date khi người dùng nói 'thứ 4 tuần này'.",
            "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"),
        ["week"] = Schema.Str("Đi kèm weekday. Mặc định this_week.", "this_week", "last_week"),
        ["shift_part"] = Schema.Str("Ca cần giải trình nếu ngày đó có nhiều ca.", "morning", "afternoon", "evening", "night"),
        ["reason"] = Schema.Str("Lý do đi muộn / về sớm do người dùng nói, vd 'kẹt xe'.")
    }, "reason");

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var date = ToolArgs.DateOrWeekday(args, "date", ctx.Today)
                   ?? DateGrounding.OnlyMentionedDate(ctx.UserText, ctx.Today)
                   ?? throw new ToolArgumentException("Thiếu ngày cần giải trình: truyền date, relative_day hoặc weekday (+ week).");
        DateGrounding.RequireUserSaidDateAndReason(ToolArgs.Str(args, "reason"), ctx.UserText, ctx.Today);
        var grounded = DateGrounding.Apply(date, date, ctx.UserText, ctx.Today);
        var (part, partNote) = ShiftPartGrounding.Apply(ToolArgs.Part(args, "shift_part"), ctx.UserText);

        var result = await _drafts.BuildExplanationDraftAsync(
            ctx.UserId, new ExplanationDraftRequest(grounded.Start, part, ToolArgs.Str(args, "reason") ?? string.Empty), ctx.ConversationId, ct);
        var tool = DraftToolResults.FromBuild(result);
        if (grounded.Note is not null && tool.Data is JsonObject o) o["date_note"] = grounded.Note;
        if (partNote is not null && tool.Data is JsonObject p) p["shift_note"] = partNote;
        return tool;
    }
}
