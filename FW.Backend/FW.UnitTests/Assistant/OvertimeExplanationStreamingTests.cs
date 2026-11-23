using FW.Application.Common.Exceptions;
using FW.Application.Features.Assistant;
using FW.Infrastructure.Services.Llm;

namespace FW.UnitTests.Assistant;

/// <summary>Phần bổ sung so với module FW.AI gốc: bản nháp OT, bản nháp giải trình và trả lời theo luồng (SSE).</summary>
public class OvertimeDraftTests
{
    // Hôm nay Thứ Tư 18/11/2026 10:00
    private static readonly DateOnly Tomorrow = new(2026, 11, 19);

    [Fact]
    public async Task Overtime_draft_carries_start_and_end_times_and_is_not_sent()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildOvertimeDraftAsync(
            3, new OvertimeDraftRequest(Tomorrow, new TimeOnly(18, 0), new TimeOnly(21, 30), "kiểm kho"), 1);

        Assert.True(result.Ok, result.Message);
        var s = result.Draft!.Submission;
        Assert.Equal(5, s.LeaveTypeId);
        Assert.Equal(new DateTime(2026, 11, 19, 18, 0, 0), s.StartAt);
        Assert.Equal(new DateTime(2026, 11, 19, 21, 30, 0), s.EndAt);
        Assert.Equal("overtime", result.Draft.Card.Kind);
        Assert.Contains(result.Draft.Card.Lines, l => l.Label == "Số giờ" && l.Value == "3,5 giờ");
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Overtime_past_midnight_ends_on_the_next_day()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildOvertimeDraftAsync(
            3, new OvertimeDraftRequest(Tomorrow, new TimeOnly(22, 0), new TimeOnly(1, 0), "chốt sổ"), null);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(new DateTime(2026, 11, 20, 1, 0, 0), result.Draft!.Submission.EndAt);
        Assert.Contains(result.Draft.Card.Lines, l => l.Value.Contains("qua nửa đêm"));
    }

    [Theory]
    [InlineData(18, 0, 18, 10)]   // dưới 30 phút
    [InlineData(6, 0, 20, 0)]     // quá 12 giờ
    public async Task Overtime_outside_the_system_limits_is_rejected(int sh, int sm, int eh, int em)
    {
        var w = new World();
        var result = await w.NewDraftService().BuildOvertimeDraftAsync(
            3, new OvertimeDraftRequest(Tomorrow, new TimeOnly(sh, sm), new TimeOnly(eh, em), "việc gấp"), null);

        Assert.False(result.Ok);
        Assert.Equal("invalid_overtime", result.Code);
    }

    [Fact]
    public async Task Overtime_needs_a_reason()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildOvertimeDraftAsync(
            3, new OvertimeDraftRequest(Tomorrow, new TimeOnly(18, 0), new TimeOnly(20, 0), "  "), null);
        Assert.Equal("missing_reason", result.Code);
    }

    [Fact]
    public async Task Overtime_tool_reads_times_in_the_usual_vietnamese_forms()
    {
        var w = new World();
        w.Llm.CallTool("draft_overtime_request",
                "{\"relative_day\":\"tomorrow\",\"start_time\":\"18h\",\"end_time\":\"21h30\",\"reason\":\"kiểm kho\"}")
            .Say("Mình đã soạn bản nháp OT, bạn bấm Xác nhận nhé.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Đăng ký OT ngày mai từ 18h đến 21h30 để kiểm kho"));

        var draft = await w.Drafts.GetAsync(Assert.Single(reply.Drafts).Id);
        Assert.Equal(new DateTime(2026, 11, 19, 18, 0, 0), draft!.Submission.StartAt);
        Assert.Equal(new DateTime(2026, 11, 19, 21, 30, 0), draft.Submission.EndAt);
    }

    [Fact]
    public async Task Confirming_an_overtime_draft_submits_the_times()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildOvertimeDraftAsync(3, new OvertimeDraftRequest(Tomorrow, new TimeOnly(18, 0), new TimeOnly(20, 0), "kiểm kho"), null)).Draft!;

        await svc.ConfirmAsync(draft.Id, 3, null);

        var sent = Assert.Single(w.Hr.Submitted);
        Assert.Equal(new DateTime(2026, 11, 19, 18, 0, 0), sent.StartAt);
    }
}

public class ExplanationDraftTests
{
    private static readonly DateOnly Yesterday = new(2026, 11, 17);

    [Fact]
    public async Task Explanation_targets_the_late_attendance_log()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildExplanationDraftAsync(3, new ExplanationDraftRequest(Yesterday, null, "kẹt xe"), 1);

        Assert.True(result.Ok, result.Message);
        var draft = result.Draft!;
        Assert.Equal(6, draft.Submission.LeaveTypeId);
        Assert.Equal(9001, draft.Submission.AttendanceLogId);
        Assert.Equal("explanation", draft.Card.Kind);
        Assert.Contains(draft.Card.Lines, l => l.Label == "Vấn đề" && l.Value.Contains("đi muộn 12 phút"));
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Today_is_accepted_but_the_future_is_not()
    {
        var w = new World();
        var future = await w.NewDraftService().BuildExplanationDraftAsync(3, new ExplanationDraftRequest(new DateOnly(2026, 11, 19), null, "x"), null);
        Assert.Equal("future_date", future.Code);
    }

    [Fact]
    public async Task Older_than_seven_days_is_rejected()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildExplanationDraftAsync(3, new ExplanationDraftRequest(new DateOnly(2026, 11, 10), null, "x"), null);
        Assert.Equal("too_old", result.Code);
    }

    [Fact]
    public async Task A_day_with_nothing_wrong_has_nothing_to_explain()
    {
        var w = new World();
        // Chỉ còn lượt đúng giờ trong ngày
        w.Hr.AttendanceLogs.RemoveAll(l => l.Log.Id == 9001);
        var result = await w.NewDraftService().BuildExplanationDraftAsync(3, new ExplanationDraftRequest(Yesterday, null, "x"), null);
        Assert.Equal("nothing_to_explain", result.Code);
    }

    [Fact]
    public async Task A_day_without_attendance_says_so()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildExplanationDraftAsync(3, new ExplanationDraftRequest(new DateOnly(2026, 11, 16), null, "x"), null);
        Assert.Equal("no_attendance_log", result.Code);
    }

    [Fact]
    public async Task Several_logs_needing_explanation_ask_which_one_unless_the_part_is_given()
    {
        var w = new World();
        w.Hr.AttendanceLogs.Add((3, new AttendanceLogDto(9003, Yesterday, "Ca chiều chuẩn", new TimeSpan(13, 0, 0),
            new DateTime(2026, 11, 17, 13, 0, 0), new DateTime(2026, 11, 17, 16, 30, 0), 0, 30, "valid", true)));
        var svc = w.NewDraftService();

        var ambiguous = await svc.BuildExplanationDraftAsync(3, new ExplanationDraftRequest(Yesterday, null, "x"), null);
        Assert.Equal("ambiguous_log", ambiguous.Code);

        var picked = await svc.BuildExplanationDraftAsync(3, new ExplanationDraftRequest(Yesterday, ShiftPart.Afternoon, "đau bụng"), null);
        Assert.True(picked.Ok, picked.Message);
        Assert.Equal(9003, picked.Draft!.Submission.AttendanceLogId);
    }

    [Fact]
    public async Task Explanation_tool_understands_yesterday()
    {
        var w = new World();
        w.Llm.CallTool("draft_explanation_request", "{\"relative_day\":\"yesterday\",\"reason\":\"kẹt xe\"}").Say("Đã soạn bản nháp giải trình.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Hôm qua em đi muộn vì kẹt xe, làm đơn giải trình giúp em"));

        var draft = await w.Drafts.GetAsync(Assert.Single(reply.Drafts).Id);
        Assert.Equal(9001, draft!.Submission.AttendanceLogId);
    }
}

public class StreamingTests
{
    private static async Task<List<AssistantEvent>> CollectAsync(IAsyncEnumerable<AssistantEvent> source)
    {
        var list = new List<AssistantEvent>();
        await foreach (var e in source) list.Add(e);
        return list;
    }

    [Fact]
    public async Task A_streamed_answer_emits_conversation_then_deltas_then_done()
    {
        var w = new World();
        w.Llm.Say("Bạn còn 11 ngày phép");

        var events = await CollectAsync(w.NewAssistant().ChatStreamAsync(new ChatRequest(null, "Em còn mấy ngày phép?")));

        Assert.IsType<ConversationEvent>(events[0]);
        var text = string.Concat(events.OfType<TextDeltaEvent>().Select(e => e.Text)).Trim();
        Assert.Equal("Bạn còn 11 ngày phép", text);
        var done = Assert.IsType<DoneEvent>(events[^1]);
        Assert.Equal("Bạn còn 11 ngày phép", done.Reply.Reply);
        Assert.Equal(((ConversationEvent)events[0]).ConversationId, done.Reply.ConversationId);

        // Đã lưu hội thoại như đường không streaming
        Assert.Equal(1, await w.Store.CountUserMessagesOnAsync(3, w.Clock.Today));
    }

    [Fact]
    public async Task A_tool_turn_announces_the_tool_and_a_draft_arrives_with_done()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"weekday\":\"thursday\",\"week\":\"this_week\",\"reason\":\"bận thi\"}")
            .Say("Đã soạn bản nháp, bạn bấm Xác nhận nhé");

        var events = await CollectAsync(w.NewAssistant().ChatStreamAsync(new ChatRequest(null, "Cho em nghỉ thứ 5 tuần này vì bận thi")));

        Assert.Contains(events, e => e is ToolStartEvent { Name: "draft_leave_request" });
        var done = events.OfType<DoneEvent>().Single();
        Assert.Single(done.Reply.Drafts);
        Assert.Empty(w.Hr.Submitted); // vẫn chưa gửi đơn
    }

    [Fact]
    public async Task Business_errors_surface_on_the_first_read_before_any_event()
    {
        var w = new World();
        var stream = w.NewAssistant().ChatStreamAsync(new ChatRequest(null, "   "));
        await Assert.ThrowsAsync<BadRequestException>(() => CollectAsync(stream));
    }

    [Fact]
    public async Task A_model_outage_during_streaming_becomes_a_service_error()
    {
        var w = new World();
        w.Llm.ThrowOnCall = new LlmUnavailableException("ollama down");
        await Assert.ThrowsAsync<ExternalServiceException>(() => CollectAsync(w.NewAssistant().ChatStreamAsync(new ChatRequest(null, "hi"))));
        Assert.Equal(0, await w.Store.CountUserMessagesOnAsync(3, w.Clock.Today));
    }
}

public class StreamTextFilterTests
{
    private static string Run(params string[] chunks)
    {
        var f = new StreamTextFilter();
        var sb = new System.Text.StringBuilder();
        foreach (var c in chunks) sb.Append(f.Push(c));
        sb.Append(f.Flush());
        return sb.ToString();
    }

    [Fact]
    public void Plain_text_passes_through_unchanged() => Assert.Equal("Xin chào bạn", Run("Xin ", "chào ", "bạn"));

    [Fact]
    public void Think_blocks_are_removed() => Assert.Equal("Đáp án", Run("<think>nghĩ ngợi</think>Đáp án"));

    [Fact]
    public void Tags_split_across_chunks_are_still_removed() =>
        Assert.Equal("ab", Run("a<th", "ink>xx</th", "ink>b"));

    [Fact]
    public void Tool_call_text_is_hidden_from_the_preview() =>
        Assert.Equal("Để mình tra. ", Run("Để mình tra. <tool_call>{\"name\":\"x\"", ",\"arguments\":{}}</tool_call>"));

    [Fact]
    public void A_lone_angle_bracket_is_kept_when_it_is_not_a_tag() => Assert.Equal("3 < 5 và 7 > 2", Run("3 < ", "5 và 7 > 2"));

    [Fact]
    public void An_unclosed_think_block_stays_hidden() => Assert.Equal("ok ", Run("ok <think>không bao giờ đóng"));
}

/// <summary>Neo bằng mã những chỗ mô hình nhỏ hay sai: quên ngày, bịa tên đồng nghiệp.</summary>
public class ToolArgumentGroundingTests
{
    [Fact]
    public async Task Overtime_without_a_date_argument_uses_the_single_day_the_user_said()
    {
        var w = new World();
        w.Llm.CallTool("draft_overtime_request", "{\"start_time\":\"18:00\",\"end_time\":\"21:00\",\"reason\":\"kiểm kho\"}").Say("Bản nháp đây.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Đăng ký giúp em OT tối mai từ 18h đến 21h để kiểm kho"));

        var draft = await w.Drafts.GetAsync(Assert.Single(reply.Drafts).Id);
        Assert.Equal(new DateOnly(2026, 11, 19), draft!.Submission.StartDate);
    }

    [Fact]
    public async Task Overtime_without_any_date_anywhere_asks_again_instead_of_guessing()
    {
        var w = new World();
        w.Llm.CallTool("draft_overtime_request", "{\"start_time\":\"18:00\",\"end_time\":\"21:00\",\"reason\":\"kiểm kho\"}").Say("Bạn muốn OT ngày nào?");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Đăng ký OT từ 18h đến 21h để kiểm kho"));

        Assert.Empty(reply.Drafts);
        Assert.Contains("Thiếu ngày", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task Swap_with_a_colleague_the_user_never_named_is_refused()
    {
        var w = new World();
        w.Llm.CallTool("draft_shift_swap_request", "{\"relative_day\":\"tomorrow\",\"colleague_name\":\"Lan\",\"reason\":\"việc riêng\"}")
             .Say("Bạn muốn đổi ca với ai?");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Đổi ca sáng ngày mai của em sang ngày kia được không? Em có việc riêng"));

        Assert.Empty(reply.Drafts);
        Assert.Contains("chưa nói muốn đổi ca", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task A_reason_the_user_never_gave_is_refused()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"weekday\":\"thursday\",\"week\":\"this_week\",\"reason\":\"bận thi\"}")
             .Say("Bạn nghỉ vì lý do gì?");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Em xin nghỉ thứ 5 tuần này"));

        Assert.Empty(reply.Drafts);
        Assert.Contains("không có trong lời người dùng", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task A_date_the_user_never_gave_is_refused()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-19\",\"reason\":\"bận việc\"}")
             .Say("Bạn muốn nghỉ ngày nào?");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Cho em nghỉ vì bận việc"));

        Assert.Empty(reply.Drafts);
        Assert.Contains("chưa nói ngày nào", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Theory]
    [InlineData("bận thi", "xin nghỉ ca sáng vì đi thi", true)]
    [InlineData("việc nhà", "em có việc nhà", true)]
    [InlineData("bận việc", "cho em nghỉ vì bận việc", true)]
    [InlineData("kiểm kho", "đăng ký OT mai từ 18h đến 20h", false)]
    [InlineData("để hoàn thành dự án", "em muốn làm thêm giờ thứ 6", false)]
    public void Reason_must_come_from_the_user(string reason, string userText, bool expected) =>
        Assert.Equal(expected, DateGrounding.ReasonWasMentioned(reason, userText));

    [Theory]
    [InlineData("Nguyễn Thị Lan", "đổi ca chiều mai với chị Lan.", true)]
    [InlineData("Lan", "Em muốn đổi ca với LAN nhé", true)]
    [InlineData("Lan", "đổi ca sáng ngày mai sang ngày kia", false)]
    [InlineData("", "đổi ca với Lan", false)]
    public void Colleague_name_must_come_from_the_user(string name, string userText, bool expected) =>
        Assert.Equal(expected, DateGrounding.NameWasMentioned(name, userText));
}
