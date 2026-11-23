using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;

namespace FW.UnitTests.Assistant;

public class DraftServiceTests
{
    // Hôm nay Thứ Tư 18/11/2026; Thứ Năm tuần này = 19/11. Nhân viên id 3 có ca sáng + ca chiều Thứ Hai–Thứ Bảy.
    private static readonly DateOnly Thursday = new(2026, 11, 19);

    private static LeaveDraftRequest Leave(DateOnly start, DateOnly? end = null, ShiftPart? part = null, string reason = "bận thi", int type = 1) =>
        new(type, start, end ?? start, part, reason);

    [Fact]
    public async Task Morning_shift_only_still_takes_the_whole_day_and_warns_about_it()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday, part: ShiftPart.Morning), 1);

        Assert.True(result.Ok, result.Message);
        var draft = result.Draft!;
        Assert.Equal(DraftStatus.Pending, draft.Status);
        Assert.Equal(3, draft.Submission.UserId);
        Assert.Equal(Thursday, draft.Submission.StartDate);
        Assert.Equal(Thursday, draft.Submission.EndDate);
        Assert.Equal(1m, draft.Submission.DurationDays); // hệ thống chỉ nhận nghỉ theo ngày
        Assert.Equal("bận thi", draft.Submission.Reason);
        Assert.Contains(draft.Card.Lines, l => l.Label == "Số ngày nghỉ" && l.Value == "1 ngày");
        Assert.Contains(draft.Card.Lines, l => l.Value.Contains("Ca sáng chuẩn") && !l.Value.Contains("Ca chiều"));
        Assert.Contains(draft.Card.Warnings, x => x.Contains("CẢ NGÀY") && x.Contains("đổi ca"));
        Assert.Equal(draft.Id, (await w.Drafts.GetAsync(draft.Id))!.Id); // đã lưu
        Assert.Empty(w.Hr.Submitted);                                    // CHƯA gửi đơn thật
    }

    [Fact]
    public async Task Whole_day_is_one_day_and_nothing_is_sent_yet()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday), null);
        Assert.Equal(1m, result.Draft!.Submission.DurationDays);
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Multi_day_range_skips_days_without_shifts()
    {
        var w = new World();
        // Thứ Bảy 21/11 → Thứ Hai 23/11: Chủ nhật 22/11 không có ca
        var result = await w.NewDraftService().BuildLeaveDraftAsync(3, Leave(new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 23)), null);
        Assert.True(result.Ok);
        Assert.Equal(2m, result.Draft!.Submission.DurationDays);
        Assert.Equal(new DateOnly(2026, 11, 21), result.Draft.Submission.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 23), result.Draft.Submission.EndDate);
    }

    [Fact]
    public async Task Past_date_is_rejected()
    {
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(3, Leave(new DateOnly(2026, 11, 17)), null);
        Assert.False(result.Ok);
        Assert.Equal("past_date", result.Code);
    }

    [Fact]
    public async Task Missing_reason_is_rejected_so_the_model_asks_instead_of_inventing_one()
    {
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday, reason: "  "), null);
        Assert.Equal("missing_reason", result.Code);
    }

    [Fact]
    public async Task Part_the_employee_does_not_work_is_rejected_with_their_real_schedule()
    {
        var w = new World();
        // Nhân viên 5 (số lẻ) làm ca chiều + tối, không có ca sáng
        var result = await w.NewDraftService().BuildLeaveDraftAsync(5, Leave(Thursday, part: ShiftPart.Morning), null);
        Assert.Equal("no_matching_shifts", result.Code);
        Assert.NotNull(result.Details!["scheduled_shifts"]);
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Day_without_any_shift_is_rejected()
    {
        var sunday = new DateOnly(2026, 11, 22);
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(3, Leave(sunday), null);
        Assert.Equal("no_matching_shifts", result.Code);
    }

    [Fact]
    public async Task Only_leave_types_are_accepted_for_leave_drafts()
    {
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday, type: 4), null); // 4 = đổi ca
        Assert.Equal("invalid_leave_type", result.Code);
        Assert.NotNull(result.Details!["valid_leave_types"]);
    }

    [Fact]
    public async Task Range_longer_than_the_limit_is_rejected()
    {
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday, Thursday.AddDays(40)), null);
        Assert.Equal("range_too_long", result.Code);
    }

    [Fact]
    public async Task Warns_when_leave_balance_is_not_enough_including_pending_requests()
    {
        var w = new World();
        w.Hr.Balances[(3, 2026)] = new LeaveBalanceDto(2026, 12m, 11.5m, 0.5m, 0m);
        var result = await w.NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday), null); // cần 1 ngày
        Assert.True(result.Ok);
        Assert.Contains(result.Draft!.Card.Warnings, x => x.Contains("Quỹ phép"));
    }

    [Fact]
    public async Task No_balance_warning_when_enough()
    {
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday), null);
        Assert.DoesNotContain(result.Draft!.Card.Warnings, x => x.Contains("Quỹ phép"));
        Assert.Contains(result.Draft.Card.Lines, l => l.Label.StartsWith("Quỹ phép"));
    }

    [Fact]
    public async Task Unpaid_leave_does_not_consume_balance_and_gets_no_balance_warning()
    {
        var w = new World();
        w.Hr.Balances[(3, 2026)] = new LeaveBalanceDto(2026, 12m, 12m, 0m, 0m);
        var result = await w.NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday, type: 3), null);
        Assert.Empty(result.Draft!.Card.Warnings);
    }

    [Fact]
    public async Task Warns_about_an_overlapping_pending_request()
    {
        var w = new World();
        await w.Hr.SubmitAsync(new LeaveRequestSubmission(3, 1, Thursday, Thursday, 1m, "x", null, null, null), null);
        var result = await w.NewDraftService().BuildLeaveDraftAsync(3, Leave(Thursday, part: ShiftPart.Afternoon), null);
        Assert.Contains(result.Draft!.Card.Warnings, x => x.Contains("đã có đơn"));
    }

    [Fact]
    public async Task Sick_leave_requires_an_attachment_before_it_can_be_confirmed()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday, type: 2, reason: "sốt"), null)).Draft!;
        Assert.True(draft.Card.RequiresAttachment);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.ConfirmAsync(draft.Id, 3, null));
        Assert.Empty(w.Hr.Submitted);

        var file = new SubmissionAttachment(new MemoryStream(new byte[] { 1, 2, 3 }), "giay-kham.jpg", "image/jpeg", 3);
        var (_, result) = await svc.ConfirmAsync(draft.Id, 3, file);
        Assert.Same(file, w.Hr.Attachments.Single());
        Assert.True(result.RequestId > 0);
    }

    // ----------------------------------------------------------------------------- Xác nhận / hủy

    [Fact]
    public async Task Confirm_sends_exactly_one_request_and_is_idempotent()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday, part: ShiftPart.Morning), null)).Draft!;

        var (confirmed, first) = await svc.ConfirmAsync(draft.Id, 3, null);
        var (_, second) = await svc.ConfirmAsync(draft.Id, 3, null);

        Assert.Equal(DraftStatus.Confirmed, confirmed.Status);
        Assert.Equal(first.RequestId, second.RequestId);
        var sent = Assert.Single(w.Hr.Submitted);
        Assert.Equal(1m, sent.DurationDays);

        var pending = await w.Hr.GetMyRequestsAsync(3, "pending", 10);
        Assert.Contains(pending, r => r.Id == first.RequestId);
    }

    [Fact]
    public async Task Concurrent_double_click_still_sends_once()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday), null)).Draft!;

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await svc.ConfirmAsync(draft.Id, 3, null); return true; }
            catch (AppException) { return false; }
        }));

        Assert.Single(w.Hr.Submitted);
        Assert.Contains(true, results);
    }

    [Fact]
    public async Task Another_user_cannot_see_or_confirm_the_draft()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday), null)).Draft!;

        await Assert.ThrowsAsync<NotFoundException>(() => svc.ConfirmAsync(draft.Id, 5, null));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CancelAsync(draft.Id, 5));
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Expired_draft_cannot_be_confirmed()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday), null)).Draft!;

        w.Clock.Now = w.Clock.Now.AddMinutes(w.Options.DraftTtlMinutes + 1);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => svc.ConfirmAsync(draft.Id, 3, null));
        Assert.Contains("hết hạn", ex.Message);
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Cancelled_draft_cannot_be_confirmed()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday), null)).Draft!;

        await svc.CancelAsync(draft.Id, 3);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.ConfirmAsync(draft.Id, 3, null));
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task A_confirmed_request_cannot_be_cancelled_through_the_draft()
    {
        var w = new World();
        var svc = w.NewDraftService();
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday), null)).Draft!;
        await svc.ConfirmAsync(draft.Id, 3, null);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CancelAsync(draft.Id, 3));
    }

    [Fact]
    public async Task Failed_submission_returns_the_draft_to_pending_so_the_user_can_retry()
    {
        var w = new World();
        var flaky = new FlakySubmitter(w.Hr);
        var svc = w.NewDraftService(flaky);
        var draft = (await svc.BuildLeaveDraftAsync(3, Leave(Thursday), null)).Draft!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConfirmAsync(draft.Id, 3, null));
        Assert.Equal(DraftStatus.Pending, (await w.Drafts.GetAsync(draft.Id))!.Status);

        await svc.ConfirmAsync(draft.Id, 3, null);
        Assert.Single(w.Hr.Submitted);
    }

    private sealed class FlakySubmitter : ILeaveRequestSubmitter
    {
        private readonly ILeaveRequestSubmitter _inner;
        private bool _failed;

        public FlakySubmitter(ILeaveRequestSubmitter inner) => _inner = inner;

        public Task<SubmissionResult> SubmitAsync(LeaveRequestSubmission s, SubmissionAttachment? attachment, CancellationToken ct = default)
        {
            if (_failed) return _inner.SubmitAsync(s, attachment, ct);
            _failed = true;
            throw new InvalidOperationException("backend down");
        }
    }

    // ----------------------------------------------------------------------------- Đổi ca

    private static SwapDraftRequest Swap(string colleague, ShiftPart? mine = ShiftPart.Afternoon, ShiftPart? theirs = ShiftPart.Afternoon,
        string reason = "có việc gia đình") => new(Thursday, mine, colleague, null, theirs, reason);

    [Fact]
    public async Task Swap_draft_links_both_schedules_and_the_colleague()
    {
        var w = new World();
        var result = await w.NewDraftService().BuildSwapDraftAsync(3, Swap("Lan"), null);

        Assert.True(result.Ok, result.Message);
        var s = result.Draft!.Submission;
        Assert.Equal(4, s.LeaveTypeId);
        Assert.Equal(5, s.TargetUserId);
        Assert.NotNull(s.SourceScheduleId);
        Assert.NotNull(s.TargetScheduleId);
        Assert.Equal(Thursday, s.StartDate);
        Assert.Contains(result.Draft.Card.Lines, l => l.Value.Contains("Nguyễn Thị Lan"));
        Assert.Empty(w.Hr.Submitted);
    }

    [Fact]
    public async Task Swap_finds_colleague_ignoring_case_and_accents()
    {
        var result = await new World().NewDraftService().BuildSwapDraftAsync(3, Swap("nguyen thi lan"), null);
        Assert.True(result.Ok, result.Message);
        Assert.Equal(5, result.Draft!.Submission.TargetUserId);
    }

    [Fact]
    public async Task Swap_never_matches_a_colleague_from_another_branch()
    {
        var result = await new World().NewDraftService().BuildSwapDraftAsync(3, Swap("Nam"), null); // Bùi Văn Nam ở chi nhánh 2
        Assert.Equal("colleague_not_found", result.Code);
    }

    [Fact]
    public async Task Swap_with_ambiguous_name_asks_the_user_to_choose()
    {
        var w = new World();
        w.Hr.Users.Add(new DemoUser(12, "EMP012", "Trần Thị Lan", 1, new[] { "ROLE_EMPLOYEE" }));
        var svc = w.NewDraftService();

        var ambiguous = await svc.BuildSwapDraftAsync(3, Swap("Lan"), null);
        Assert.Equal("ambiguous_colleague", ambiguous.Code);

        var exact = await svc.BuildSwapDraftAsync(3, Swap("Nguyễn Thị Lan"), null);
        Assert.True(exact.Ok, exact.Message);
        Assert.Equal(5, exact.Draft!.Submission.TargetUserId);
    }

    [Fact]
    public async Task Swap_with_a_colleague_who_has_no_shift_that_day_is_a_one_way_handover_with_a_warning()
    {
        var w = new World();
        // Nhân viên 5 không có ca sáng → đổi "ca chiều của tôi" lấy "ca sáng của Lan" không có đích
        var result = await w.NewDraftService().BuildSwapDraftAsync(3, Swap("Lan", ShiftPart.Afternoon, ShiftPart.Morning), null);
        Assert.True(result.Ok, result.Message);
        Assert.Null(result.Draft!.Submission.TargetScheduleId);
        Assert.Contains(result.Draft.Card.Warnings, x => x.Contains("không đổi lại"));
    }

    [Fact]
    public async Task Swap_with_two_shifts_that_day_asks_which_one()
    {
        var result = await new World().NewDraftService().BuildSwapDraftAsync(3, Swap("Lan", mine: null), null);
        Assert.Equal("ambiguous_shift", result.Code); // nhân viên 3 có cả ca sáng và chiều
    }

    [Fact]
    public async Task Swap_requires_a_reason_and_a_future_date()
    {
        var svc = new World().NewDraftService();
        Assert.Equal("missing_reason", (await svc.BuildSwapDraftAsync(3, Swap("Lan", reason: ""), null)).Code);
        Assert.Equal("past_date",
            (await svc.BuildSwapDraftAsync(3, new SwapDraftRequest(new DateOnly(2026, 11, 10), ShiftPart.Afternoon, "Lan", null, null, "x"), null)).Code);
    }
}

public class DraftDefaultsTests
{
    private static readonly DateOnly Thursday = new(2026, 11, 19);

    [Fact]
    public async Task Missing_leave_type_defaults_to_annual_leave()
    {
        var result = await new World().NewDraftService().BuildLeaveDraftAsync(
            3, new LeaveDraftRequest(null, Thursday, Thursday, ShiftPart.Morning, "bận thi"), null);
        Assert.True(result.Ok, result.Message);
        Assert.Equal(1, result.Draft!.Submission.LeaveTypeId);
    }

    [Fact]
    public async Task Swap_without_target_part_matches_the_same_part_as_the_users_shift()
    {
        // Lan (5) có ca chiều + ca tối thứ 5; người dùng đổi "ca chiều" và không nói ca bên Lan
        var w = new World();
        var result = await w.NewDraftService().BuildSwapDraftAsync(
            3, new SwapDraftRequest(Thursday, ShiftPart.Afternoon, "Lan", null, null, "việc nhà"), null);

        Assert.True(result.Ok, result.Message);
        Assert.NotNull(result.Draft!.Submission.TargetScheduleId);
        var lanAfternoon = (await w.Hr.GetMyScheduleAsync(5, Thursday, Thursday)).Single(s => s.StartTime.Hours == 13);
        Assert.Equal(lanAfternoon.ScheduleId, result.Draft.Submission.TargetScheduleId);
    }

    [Fact]
    public async Task Overlong_date_hint_lists_a_contiguous_range_suggestion()
    {
        var ex = Assert.Throws<ToolArgumentException>(() =>
            DateGrounding.Apply(new DateOnly(2026, 11, 25), new DateOnly(2026, 11, 25), "nghỉ thứ 5 và thứ 6 tuần này", new DateOnly(2026, 11, 18)));
        Assert.Contains("start_date=2026-11-19, end_date=2026-11-20", ex.Message);
        await Task.CompletedTask;
    }
}
