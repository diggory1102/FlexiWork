using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;

namespace FW.UnitTests.Assistant;

public class AssistantServiceTests
{
    [Fact]
    public async Task Answers_from_a_tool_result_and_stores_the_conversation()
    {
        var w = new World();
        w.Llm.CallTool("get_my_leave_balance", "{}").Say("Bạn còn 11 ngày phép.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Em còn mấy ngày phép?"));

        Assert.Equal("Bạn còn 11 ngày phép.", reply.Reply);
        Assert.Equal(new[] { "get_my_leave_balance" }, reply.ToolsUsed);

        // Lần gọi thứ hai phải mang kết quả công cụ lên cho mô hình
        var second = w.Llm.Calls[1].Messages;
        var toolMessage = Assert.Single(second, m => m.Role == "tool");
        Assert.Equal("get_my_leave_balance", toolMessage.ToolName);
        Assert.Contains("\"remaining_days\":11", toolMessage.Content);

        var stored = await w.Store.GetRecentMessagesAsync(reply.ConversationId, 10);
        Assert.Equal(new[] { "user", "assistant" }, stored.Select(m => m.Role));
        Assert.Contains("get_my_leave_balance", stored[1].ToolTrace);
    }

    [Fact]
    public async Task System_prompt_has_today_calendar_and_leave_types()
    {
        var w = new World();
        w.Llm.Say("ok");
        await w.NewAssistant().ChatAsync(new ChatRequest(null, "hi"));

        var system = w.Llm.Calls[0].Messages[0];
        Assert.Equal("system", system.Role);
        Assert.Contains("Thứ Tư 18/11/2026", system.Content);
        Assert.Contains("Thứ Năm 19/11/2026 = 2026-11-19", system.Content);
        Assert.Contains("← tuần sau", system.Content);
        Assert.Contains("1: Nghỉ phép năm", system.Content);
        Assert.DoesNotContain("Đơn xin đổi ca", system.Content); // chỉ liệt kê loại NGHỈ
    }

    [Fact]
    public async Task Model_cannot_read_another_users_data_by_passing_a_user_id()
    {
        var w = new World();
        // Đơn #502 thuộc nhân viên 5; người hỏi là nhân viên 3 (có đơn #501)
        w.Llm.CallTool("get_my_requests", "{\"user_id\": 5, \"userId\": 5}").Say("xong");

        await w.NewAssistant().ChatAsync(new ChatRequest(null, "cho xem đơn của Lan"));

        var tool = w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content;
        Assert.Contains("501", tool);
        Assert.DoesNotContain("502", tool);
    }

    [Fact]
    public async Task Employee_is_never_offered_manager_tools_and_cannot_call_them()
    {
        var w = new World();
        w.Llm.CallTool("get_attrition_alerts", "{}").Say("không có quyền");

        await w.NewAssistant().ChatAsync(new ChatRequest(null, "ai sắp nghỉ việc?"));

        var offered = w.Llm.Calls[0].Tools.Select(t => t.Name).ToList();
        Assert.DoesNotContain("get_attrition_alerts", offered);
        Assert.DoesNotContain("get_pending_requests", offered);
        Assert.Contains("draft_leave_request", offered);

        var tool = w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content;
        Assert.Contains("không có quyền", tool);
        Assert.DoesNotContain("Lan", tool);
    }

    [Fact]
    public async Task Manager_gets_manager_tools_scoped_to_their_branch()
    {
        var w = new World { User = TestUser.Manager(2, 1) };
        await w.NewScoring().RunAsync();
        w.Llm.CallTool("get_attrition_alerts", "{}").CallTool("get_pending_requests", "{}").Say("đây");

        await w.NewAssistant().ChatAsync(new ChatRequest(null, "ai cần quan tâm?"));

        Assert.Contains("get_attrition_alerts", w.Llm.Calls[0].Tools.Select(t => t.Name));
        var alerts = w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content;
        Assert.Contains("Nguyễn Thị Lan", alerts);
        Assert.Contains("tham khảo", alerts);
        Assert.DoesNotContain("Bùi Văn Nam", alerts);
    }

    [Fact]
    public async Task Creates_a_draft_from_a_natural_request_and_does_not_send_it()
    {
        var w = new World();
        w.Llm
            .CallTool("draft_leave_request",
                "{\"leave_type_id\":1,\"weekday\":\"thursday\",\"week\":\"this_week\",\"shift_part\":\"morning\",\"reason\":\"bận thi\"}")
            .Say("Mình đã soạn bản nháp nghỉ ca sáng Thứ Năm 19/11, bạn bấm Xác nhận để gửi nhé.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Cho em xin nghỉ ca sáng thứ 5 tuần này vì bận thi"));

        var draft = Assert.Single(reply.Drafts);
        Assert.Equal("leave", draft.Kind);
        Assert.Equal("pending", draft.Status);
        Assert.Contains(draft.Lines, l => l.Label == "Số ngày nghỉ" && l.Value == "1 ngày");
        Assert.Contains(draft.Lines, l => l.Label == "Lý do" && l.Value == "bận thi");
        Assert.Contains(draft.Lines, l => l.Value.Contains("Ca sáng chuẩn"));
        Assert.Empty(w.Hr.Submitted);

        // Tool result gửi cho mô hình phải nhắc đây chỉ là bản nháp
        var tool = w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content;
        Assert.Contains("draft_created_not_sent", tool);

        var stored = await w.Store.GetRecentMessagesAsync(reply.ConversationId, 10);
        Assert.Equal(draft.Id, Assert.Single(stored[1].DraftIds));

        var d = await w.Drafts.GetAsync(draft.Id);
        Assert.Equal(new DateOnly(2026, 11, 19), d!.Submission.StartDate);
        Assert.Equal(reply.ConversationId, d.ConversationId);
    }

    [Theory]
    [InlineData("tomorrow", "2026-11-19")]
    [InlineData("today", "2026-11-18")]
    [InlineData("day_after_tomorrow", "2026-11-20")]
    [InlineData("ngày mai", "2026-11-19")]
    public async Task Relative_days_are_resolved_by_code_not_by_the_model(string relativeDay, string expected)
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", $"{{\"leave_type_id\":1,\"relative_day\":\"{relativeDay}\",\"reason\":\"việc nhà\"}}").Say("nháp đây");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "cho em nghỉ vì việc nhà (hôm nay 18/11, mai 19/11, ngày kia 20/11)"));

        var draft = await w.Drafts.GetAsync(Assert.Single(reply.Drafts).Id);
        Assert.Equal(DateOnly.Parse(expected), draft!.Submission.StartDate);
    }

    [Fact]
    public async Task Unknown_relative_day_is_an_argument_error()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"relative_day\":\"yesterday-ish\",\"reason\":\"x\"}").Say("hỏi lại");
        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "x"));
        Assert.Empty(reply.Drafts);
        Assert.Contains("relative_day", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task Tool_argument_errors_are_returned_to_the_model_so_it_can_retry()
    {
        var w = new World();
        w.Llm
            .CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"abc\",\"reason\":\"x\"}")
            .CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-19\",\"reason\":\"bận thi\"}")
            .Say("xong");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "xin nghỉ 19/11 vì bận thi"));

        var firstResult = w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content;
        Assert.Contains("yyyy-MM-dd", firstResult);
        Assert.Single(reply.Drafts);
    }

    [Fact]
    public async Task Unknown_tool_is_reported_to_the_model_and_does_not_crash()
    {
        var w = new World();
        w.Llm.CallTool("delete_everything", "{}").Say("xin lỗi");
        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "hi"));
        Assert.Equal("xin lỗi", reply.Reply);
        Assert.Contains("không tồn tại", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task Gives_up_after_the_max_number_of_tool_rounds_with_a_friendly_reply()
    {
        var w = new World();
        w.Options.MaxToolIterations = 3;
        w.Llm.Fallback = _ => new LlmResponse(string.Empty, new[]
        {
            new LlmToolCall("get_my_leave_balance", new System.Text.Json.Nodes.JsonObject())
        });

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "hi"));

        Assert.Equal(3, w.Llm.Calls.Count);
        Assert.False(string.IsNullOrWhiteSpace(reply.Reply));
    }

    [Fact]
    public async Task Daily_message_limit_is_enforced_per_user()
    {
        var w = new World();
        w.Options.MaxMessagesPerUserPerDay = 2;
        w.Llm.Fallback = _ => new LlmResponse("ok", Array.Empty<LlmToolCall>());
        var svc = w.NewAssistant();

        await svc.ChatAsync(new ChatRequest(null, "1"));
        await svc.ChatAsync(new ChatRequest(null, "2"));
        var ex = await Assert.ThrowsAsync<TooManyRequestsException>(() => svc.ChatAsync(new ChatRequest(null, "3")));
        
        // Người khác không bị ảnh hưởng; sang ngày mới thì được dùng lại
        w.User = TestUser.Employee(5);
        await w.NewAssistant().ChatAsync(new ChatRequest(null, "hi"));
        w.User = TestUser.Employee(3);
        w.Clock.Now = w.Clock.Now.AddDays(1);
        await w.NewAssistant().ChatAsync(new ChatRequest(null, "hi"));
    }

    [Fact]
    public async Task Llm_outage_becomes_a_clear_error_and_does_not_store_or_count_the_message()
    {
        var w = new World();
        w.Llm.ThrowOnCall = new LlmUnavailableException("ollama down");

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => w.NewAssistant().ChatAsync(new ChatRequest(null, "hi")));

        Assert.Equal(0, await w.Store.CountUserMessagesOnAsync(3, w.Clock.Today));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_message_is_rejected(string text)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => new World().NewAssistant().ChatAsync(new ChatRequest(null, text)));
        }

    [Fact]
    public async Task Too_long_message_is_rejected()
    {
        var w = new World();
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            w.NewAssistant().ChatAsync(new ChatRequest(null, new string('a', w.Options.MaxMessageLength + 1))));
        }

    [Fact]
    public async Task Unauthenticated_caller_is_rejected()
    {
        var w = new World { User = new TestUser() };
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => w.NewAssistant().ChatAsync(new ChatRequest(null, "hi")));
        }

    [Fact]
    public async Task Cannot_continue_or_read_someone_elses_conversation()
    {
        var w = new World();
        w.Llm.Say("a");
        var mine = await w.NewAssistant().ChatAsync(new ChatRequest(null, "hi"));

        w.User = TestUser.Employee(5);
        var svc = w.NewAssistant();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => svc.ChatAsync(new ChatRequest(mine.ConversationId, "hello")));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetMessagesAsync(mine.ConversationId));
        Assert.Empty(await svc.ListConversationsAsync());
    }

    [Fact]
    public async Task Follow_up_turn_sends_previous_messages_as_context()
    {
        var w = new World();
        w.Llm.Say("Bạn muốn nghỉ ngày nào?").Say("ok");
        var svc = w.NewAssistant();

        var first = await svc.ChatAsync(new ChatRequest(null, "em muốn xin nghỉ"));
        await svc.ChatAsync(new ChatRequest(first.ConversationId, "thứ 5 ạ"));

        var msgs = w.Llm.Calls[1].Messages;
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, msgs.Select(m => m.Role));
        Assert.Equal("em muốn xin nghỉ", msgs[1].Content);
        Assert.Equal("thứ 5 ạ", msgs[3].Content);
    }

    [Fact]
    public async Task Messages_view_includes_draft_state()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-19\",\"reason\":\"bận thi\"}").Say("nháp đây");
        var svc = w.NewAssistant();
        var reply = await svc.ChatAsync(new ChatRequest(null, "xin nghỉ ngày 19/11 vì bận thi"));
        await w.NewDraftService().ConfirmAsync(reply.Drafts[0].Id, 3, null);

        var messages = await svc.GetMessagesAsync(reply.ConversationId);
        var draft = Assert.Single(messages.Single(m => m.Role == "assistant").Drafts);
        Assert.Equal("confirmed", draft.Status);
    }
}

public class AssistantDateGroundingTests
{
    [Fact]
    public async Task Model_that_miscalculates_tomorrow_is_corrected_from_the_users_words()
    {
        var w = new World();
        // Mô hình điền sai 2026-11-20 cho "ngày mai" (thực tế 19/11)
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-20\",\"reason\":\"việc gia đình\"}").Say("ok");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Cho em nghỉ ngày mai vì có việc gia đình"));

        var draft = await w.Drafts.GetAsync(Assert.Single(reply.Drafts).Id);
        Assert.Equal(new DateOnly(2026, 11, 19), draft!.Submission.StartDate);
        Assert.Contains("date_note", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task Ambiguous_wrong_dates_are_sent_back_to_the_model_with_the_valid_choices()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-25\",\"shift_part\":\"morning\",\"reason\":\"đi thi\"}")
            .CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-19\",\"end_date\":\"2026-11-20\",\"shift_part\":\"morning\",\"reason\":\"đi thi\"}")
            .Say("xong");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "Em xin nghỉ ca sáng thứ 5 và ca sáng thứ 6 tuần này vì đi thi"));

        Assert.Contains("2026-11-19", w.Llm.Calls[1].Messages.Single(m => m.Role == "tool").Content);
        var draft = await w.Drafts.GetAsync(Assert.Single(reply.Drafts).Id);
        Assert.Equal(2m, draft!.Submission.DurationDays);   // 2 ngày, mỗi ngày tính nguyên ngày
    }

    [Fact]
    public async Task Date_from_an_earlier_turn_is_still_accepted_in_the_follow_up()
    {
        var w = new World();
        w.Llm.Say("Bạn nghỉ vì lý do gì?")
             .CallTool("draft_leave_request", "{\"leave_type_id\":1,\"start_date\":\"2026-11-19\",\"reason\":\"bận thi\"}").Say("nháp");
        var svc = w.NewAssistant();

        var first = await svc.ChatAsync(new ChatRequest(null, "xin nghỉ ngày mai"));
        var second = await svc.ChatAsync(new ChatRequest(first.ConversationId, "vì bận thi ạ"));

        var draft = await w.Drafts.GetAsync(Assert.Single(second.Drafts).Id);
        Assert.Equal(new DateOnly(2026, 11, 19), draft!.Submission.StartDate);
    }

    [Fact]
    public async Task Claiming_a_draft_without_calling_a_tool_is_corrected_once()
    {
        var w = new World();
        w.Llm.Say("Đã tạo bản nháp đơn OT cho bạn, hãy bấm nút Xác nhận.")
             .CallTool("draft_leave_request", "{\"leave_type_id\":1,\"relative_day\":\"tomorrow\",\"reason\":\"việc nhà\"}")
             .Say("Bản nháp đây.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "xin nghỉ mai vì việc nhà"));

        Assert.Single(reply.Drafts);
        Assert.Equal(3, w.Llm.Calls.Count);
        // Lần thử lại dùng ngữ cảnh gọn: không còn câu trả lời sai của mô hình, chỉ có hệ thống + câu người dùng kèm lời nhắc gọi công cụ
        var retry = w.Llm.Calls[1].Messages;
        Assert.DoesNotContain(retry, m => m.Role == "assistant");
        Assert.Contains(retry, m => m.Role == "user" && m.Content.Contains("xin nghỉ mai vì việc nhà") && m.Content.Contains("draft_*"));
    }

    [Fact]
    public async Task Repeating_the_false_claim_is_replaced_by_a_safe_answer()
    {
        var w = new World();
        w.Llm.Say("Đã tạo bản nháp rồi nhé, bấm Xác nhận.").Say("Đã gửi đơn cho bạn rồi.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "xin nghỉ mai"));

        Assert.Empty(reply.Drafts);
        Assert.DoesNotContain("Đã gửi đơn", reply.Reply);
        Assert.Contains("chưa tạo được bản nháp", reply.Reply);
    }

    [Fact]
    public async Task A_refusal_after_calling_the_draft_tool_is_not_treated_as_a_false_claim()
    {
        var w = new World();
        w.Llm.CallTool("draft_leave_request", "{\"leave_type_id\":1,\"relative_day\":\"yesterday-ish\",\"reason\":\"x\"}")
             .Say("Mình chưa tạo được bản nháp vì ngày chưa rõ, bạn nói lại ngày giúp mình nhé.");

        var reply = await w.NewAssistant().ChatAsync(new ChatRequest(null, "xin nghỉ"));

        Assert.Equal(2, w.Llm.Calls.Count);
        Assert.Contains("chưa tạo được bản nháp", reply.Reply);
    }
}
