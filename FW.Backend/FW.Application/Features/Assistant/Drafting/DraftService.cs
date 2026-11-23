using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.Application.Features.Assistant;

public sealed record LeaveDraftRequest(int? LeaveTypeId, DateOnly StartDate, DateOnly EndDate, ShiftPart? Part, string Reason);

public sealed record SwapDraftRequest(
    DateOnly Date, ShiftPart? Part, string ColleagueName, DateOnly? TargetDate, ShiftPart? TargetPart, string Reason);

public sealed record OvertimeDraftRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Reason);

public sealed record ExplanationDraftRequest(DateOnly Date, ShiftPart? Part, string Reason);

public sealed class DraftBuildResult
{
    public AssistantDraft? Draft { get; private init; }
    public string? Code { get; private init; }
    public string? Message { get; private init; }
    public JsonObject? Details { get; private init; }
    public bool Ok => Draft is not null;

    public static DraftBuildResult Success(AssistantDraft draft) => new() { Draft = draft };

    public static DraftBuildResult Fail(string code, string message, JsonObject? details = null) =>
        new() { Code = code, Message = message, Details = details };
}

/// <summary>
/// Dựng BẢN NHÁP đơn từ tham số mô hình trích xuất và kiểm tra nghiệp vụ trước khi cho người dùng xác nhận.
/// Chỉ xác nhận (<see cref="ConfirmAsync"/>) mới gọi <see cref="ILeaveRequestSubmitter"/> — trợ lý không tự gửi đơn.
/// </summary>
public sealed class DraftService
{
    private readonly IHrDataService _hr;
    private readonly IDraftStore _drafts;
    private readonly ILeaveRequestSubmitter _submitter;
    private readonly IDateTimeProvider _clock;
    private readonly AssistantOptions _options;

    public DraftService(
        IHrDataService hr,
        IDraftStore drafts,
        ILeaveRequestSubmitter submitter,
        IDateTimeProvider clock,
        IOptions<AssistantOptions> options)
    {
        _hr = hr;
        _drafts = drafts;
        _submitter = submitter;
        _clock = clock;
        _options = options.Value;
    }

    // ------------------------------------------------------------------------------------------------
    // Nháp đơn nghỉ
    // ------------------------------------------------------------------------------------------------
    public async Task<DraftBuildResult> BuildLeaveDraftAsync(
        int userId, LeaveDraftRequest req, long? conversationId, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var reason = (req.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return DraftBuildResult.Fail("missing_reason", "Chưa có lý do nghỉ. Hãy hỏi người dùng lý do rồi gọi lại.");
        if (reason.Length > 500) reason = reason[..500];

        if (req.StartDate < today)
            return DraftBuildResult.Fail("past_date",
                $"Ngày {Vi.Date(req.StartDate)} đã qua (hôm nay là {Vi.DateWithWeekday(today)}). Chỉ tạo đơn từ hôm nay trở đi.");
        if (req.EndDate < req.StartDate)
            return DraftBuildResult.Fail("invalid_range", "Ngày kết thúc đứng trước ngày bắt đầu.");
        if (req.EndDate.DayNumber - req.StartDate.DayNumber + 1 > _options.MaxLeaveSpanDays)
            return DraftBuildResult.Fail("range_too_long", $"Một đơn chỉ nghỉ tối đa {_options.MaxLeaveSpanDays} ngày liên tiếp.");

        var types = await _hr.GetLeaveTypesAsync(ct);
        // Mô hình nhỏ hay bỏ sót loại nghỉ → mặc định nghỉ phép năm (loại trừ quỹ phép), không có thì loại nghỉ đầu tiên
        var leaveTypes = types.Where(t => t.Category == RequestKind.Leave).ToList();
        var type = req.LeaveTypeId is { } wanted
            ? leaveTypes.FirstOrDefault(t => t.Id == wanted)
            : leaveTypes.FirstOrDefault(t => t.ConsumesBalance) ?? leaveTypes.FirstOrDefault();
        if (type is null)
        {
            var valid = new JsonArray();
            foreach (var t in types.Where(t => t.Category == RequestKind.Leave))
                valid.Add(new JsonObject { ["id"] = t.Id, ["name"] = t.Name });
            return DraftBuildResult.Fail("invalid_leave_type",
                "leave_type_id không hợp lệ. Chọn một trong các loại nghỉ bên dưới.",
                new JsonObject { ["valid_leave_types"] = valid });
        }

        var schedule = await _hr.GetMyScheduleAsync(userId, req.StartDate, req.EndDate, ct);
        var byDate = schedule.GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ToList());

        var warnings = new List<string>();
        var lines = new List<DraftLine>();
        var matchedDates = new List<DateOnly>();
        decimal duration = 0m;

        for (var d = req.StartDate; d <= req.EndDate; d = d.AddDays(1))
        {
            if (!byDate.TryGetValue(d, out var dayShifts)) continue;

            var selected = req.Part is { } part
                ? dayShifts.Where(s => ShiftPartClassifier.Classify(s.StartTime) == part).ToList()
                : dayShifts;

            if (selected.Count == 0)
            {
                warnings.Add($"{Vi.DateWithWeekday(d)}: bạn không có {ShiftPartClassifier.Label(req.Part!.Value)}, bỏ qua ngày này.");
                continue;
            }

            matchedDates.Add(d);
            duration += 1m; // hệ thống tính nghỉ theo NGÀY (RequestRules.LeaveDays), không có nửa ngày
            if (req.Part is not null && selected.Count < dayShifts.Count)
                warnings.Add($"{Vi.DateWithWeekday(d)}: hệ thống chỉ nhận đơn nghỉ theo ngày nên đơn này nghỉ CẢ NGÀY, gồm cả các ca khác trong ngày. " +
                             "Nếu chỉ muốn nhường một ca, hãy dùng đơn đổi ca.");
            lines.Add(new DraftLine(
                Vi.DateWithWeekday(d),
                string.Join("; ", selected.Select(s => $"{s.ShiftName} ({Vi.Time(s.StartTime)}–{Vi.Time(s.EndTime)})"))));
        }

        if (matchedDates.Count == 0)
        {
            var details = new JsonObject
            {
                ["scheduled_shifts"] = new JsonArray(schedule
                    .Select(s => (JsonNode)new JsonObject
                    {
                        ["date"] = s.Date.ToString("yyyy-MM-dd"),
                        ["shift"] = s.ShiftName,
                        ["start"] = Vi.Time(s.StartTime),
                        ["end"] = Vi.Time(s.EndTime)
                    }).ToArray())
            };
            return DraftBuildResult.Fail("no_matching_shifts",
                req.Part is { } p
                    ? $"Bạn không có {ShiftPartClassifier.Label(p)} nào đã công bố trong khoảng {Vi.Date(req.StartDate)} – {Vi.Date(req.EndDate)}."
                    : $"Bạn không có ca làm nào đã công bố trong khoảng {Vi.Date(req.StartDate)} – {Vi.Date(req.EndDate)}.",
                details);
        }

        var first = matchedDates.Min();
        var last = matchedDates.Max();

        // Trùng đơn đã có (chỉ cảnh báo — còn có thể xin nghỉ ca khác trong cùng ngày)
        var existing = await _hr.GetMyRequestsAsync(userId, null, 50, ct);
        foreach (var e in existing.Where(e =>
                     e.Category == RequestKind.Leave
                     && (e.Status is "pending" or "approved")
                     && e.StartDate <= last && e.EndDate >= first))
        {
            warnings.Add($"Bạn đã có đơn #{e.Id} ({e.LeaveTypeName}, {StatusVi(e.Status)}) trùng thời gian {RangeText(e.StartDate, e.EndDate)}.");
        }

        // Quỹ phép
        if (type.ConsumesBalance)
        {
            var balance = await _hr.GetMyLeaveBalanceAsync(userId, first.Year, ct);
            if (balance is null)
            {
                warnings.Add($"Chưa có quỹ phép năm {first.Year} cho bạn — nhờ HR kiểm tra.");
            }
            else
            {
                var available = balance.RemainingDays - balance.PendingDays;
                lines.Add(new DraftLine("Quỹ phép năm " + first.Year,
                    $"còn {Vi.Num(balance.RemainingDays)} ngày (đang chờ duyệt {Vi.Num(balance.PendingDays)} ngày)"));
                if (available < duration)
                    warnings.Add($"Quỹ phép khả dụng chỉ còn {Vi.Num(available)} ngày, đơn này cần {Vi.Num(duration)} ngày — phần vượt có thể bị tính nghỉ không lương.");
            }
        }

        if (type.RequiresAttachment)
            warnings.Add("Loại đơn này bắt buộc kèm giấy/ảnh minh chứng — hãy đính kèm khi xác nhận.");

        lines.Insert(0, new DraftLine("Loại đơn", type.Name + (type.IsPaid ? " (hưởng lương)" : " (không lương)")));
        lines.Insert(1, new DraftLine("Thời gian", RangeText(first, last)));
        lines.Insert(2, new DraftLine("Số ngày nghỉ", $"{Vi.Num(duration)} ngày"));
        lines.Add(new DraftLine("Lý do", reason));

        var draft = NewDraft(
            userId, conversationId,
            new DraftCard("leave", "Đơn xin nghỉ — bản nháp", lines, warnings, type.RequiresAttachment),
            new LeaveRequestSubmission(userId, type.Id, first, last, duration, reason, null, null, null));

        await _drafts.SaveAsync(draft, ct);
        return DraftBuildResult.Success(draft);
    }

    // ------------------------------------------------------------------------------------------------
    // Nháp đơn đổi ca
    // ------------------------------------------------------------------------------------------------
    public async Task<DraftBuildResult> BuildSwapDraftAsync(
        int userId, SwapDraftRequest req, long? conversationId, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var reason = (req.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return DraftBuildResult.Fail("missing_reason", "Chưa có lý do đổi ca. Hãy hỏi người dùng lý do rồi gọi lại.");
        if (reason.Length > 500) reason = reason[..500];

        var targetDate = req.TargetDate ?? req.Date;
        if (req.Date < today || targetDate < today)
            return DraftBuildResult.Fail("past_date", $"Không đổi ca được cho ngày đã qua (hôm nay là {Vi.DateWithWeekday(today)}).");
        if (string.IsNullOrWhiteSpace(req.ColleagueName))
            return DraftBuildResult.Fail("missing_colleague", "Chưa biết đổi ca với ai. Hãy hỏi tên đồng nghiệp.");

        var types = await _hr.GetLeaveTypesAsync(ct);
        var type = types.FirstOrDefault(t => t.Category == RequestKind.ShiftSwap);
        if (type is null)
            return DraftBuildResult.Fail("swap_not_supported", "Hệ thống chưa cấu hình loại đơn đổi ca.");

        var mine = (await _hr.GetMyScheduleAsync(userId, req.Date, req.Date, ct))
            .Where(s => req.Part is null || ShiftPartClassifier.Classify(s.StartTime) == req.Part)
            .OrderBy(s => s.StartTime).ToList();
        if (mine.Count == 0)
            return DraftBuildResult.Fail("no_matching_shifts", $"Bạn không có ca nào cần đổi vào {Vi.DateWithWeekday(req.Date)}.");
        if (mine.Count > 1)
            return DraftBuildResult.Fail("ambiguous_shift",
                "Ngày đó bạn có nhiều ca, hỏi người dùng muốn đổi ca nào (sáng/chiều/tối/đêm).",
                new JsonObject { ["shifts"] = new JsonArray(mine.Select(s => (JsonNode)ShiftJson(s)).ToArray()) });
        var source = mine[0];

        var candidates = await _hr.FindColleaguesAsync(userId, req.ColleagueName, ct);
        if (candidates.Count == 0)
            return DraftBuildResult.Fail("colleague_not_found", $"Không tìm thấy đồng nghiệp cùng chi nhánh tên \"{req.ColleagueName}\".");
        ColleagueDto colleague;
        if (candidates.Count == 1)
        {
            colleague = candidates[0];
        }
        else
        {
            var wanted = RelativeDateResolver.Normalize(req.ColleagueName);
            var exact = candidates.Where(c => RelativeDateResolver.Normalize(c.FullName) == wanted).ToList();
            if (exact.Count != 1)
                return DraftBuildResult.Fail("ambiguous_colleague", "Có nhiều đồng nghiệp trùng tên, hỏi người dùng chọn ai.",
                    new JsonObject
                    {
                        ["candidates"] = new JsonArray(candidates.Take(5)
                            .Select(c => (JsonNode)new JsonObject { ["name"] = c.FullName, ["employee_code"] = c.EmployeeCode }).ToArray())
                    });
            colleague = exact[0];
        }

        var colleagueShifts = (await _hr.GetColleagueShiftsForSwapAsync(userId, colleague.UserId, targetDate, ct))
            .OrderBy(s => s.StartTime).ToList();
        // Không nói ca bên đồng nghiệp → mặc định CÙNG BUỔI với ca của mình ("đổi ca chiều với Lan");
        // nếu đồng nghiệp không có ca buổi đó thì xét mọi ca của họ trong ngày.
        var wantedPart = req.TargetPart ?? ShiftPartClassifier.Classify(source.StartTime);
        var theirs = colleagueShifts.Where(s => ShiftPartClassifier.Classify(s.StartTime) == wantedPart).ToList();
        if (theirs.Count == 0 && req.TargetPart is null) theirs = colleagueShifts;
        if (theirs.Count > 1)
            return DraftBuildResult.Fail("ambiguous_target_shift",
                $"{colleague.FullName} có nhiều ca ngày {Vi.Date(targetDate)}, hỏi người dùng muốn nhận ca nào.",
                new JsonObject { ["shifts"] = new JsonArray(theirs.Select(s => (JsonNode)ShiftJson(s)).ToArray()) });
        var target = theirs.Count == 1 ? theirs[0] : null;

        var warnings = new List<string> { "Đơn sẽ được chuyển Quản lý duyệt; ca chỉ đổi khi đơn được duyệt." };
        if (target is null)
            warnings.Add($"{colleague.FullName} không có ca ngày {Vi.Date(targetDate)} — đây là đề nghị chuyển ca của bạn cho {colleague.FullName}, không đổi lại ca nào.");

        var lines = new List<DraftLine>
        {
            new("Loại đơn", type.Name),
            new("Ca của bạn", $"{Vi.DateWithWeekday(source.Date)} · {ShiftText(source)}"),
            new("Đổi với", $"{colleague.FullName} ({colleague.EmployeeCode})"),
            new($"Ca của {colleague.FullName}", target is null ? $"{Vi.DateWithWeekday(targetDate)} · (không có ca)" : $"{Vi.DateWithWeekday(target.Date)} · {ShiftText(target)}"),
            new("Lý do", reason)
        };

        var draft = NewDraft(
            userId, conversationId,
            new DraftCard("shift_swap", "Đơn xin đổi ca — bản nháp", lines, warnings, type.RequiresAttachment),
            new LeaveRequestSubmission(userId, type.Id, source.Date, source.Date, 0m, reason,
                colleague.UserId, source.ScheduleId, target?.ScheduleId));

        await _drafts.SaveAsync(draft, ct);
        return DraftBuildResult.Success(draft);
    }

    // ------------------------------------------------------------------------------------------------
    // Nháp đơn làm thêm giờ (OT)
    // ------------------------------------------------------------------------------------------------
    public async Task<DraftBuildResult> BuildOvertimeDraftAsync(
        int userId, OvertimeDraftRequest req, long? conversationId, CancellationToken ct = default)
    {
        var reason = (req.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return DraftBuildResult.Fail("missing_reason", "Chưa có lý do làm thêm giờ. Hãy hỏi người dùng lý do rồi gọi lại.");
        if (reason.Length > 500) reason = reason[..500];

        // Giờ kết thúc <= giờ bắt đầu nghĩa là OT qua nửa đêm
        var startAt = req.Date.ToDateTime(req.StartTime);
        var endAt = req.Date.ToDateTime(req.EndTime);
        if (endAt <= startAt) endAt = endAt.AddDays(1);

        var invalid = RequestRules.ValidateOvertime(startAt, endAt, _clock.Now);
        if (invalid is not null) return DraftBuildResult.Fail("invalid_overtime", invalid);

        var types = await _hr.GetLeaveTypesAsync(ct);
        var type = types.FirstOrDefault(t => t.Category == RequestKind.Overtime);
        if (type is null)
            return DraftBuildResult.Fail("overtime_not_supported", "Hệ thống chưa cấu hình loại đơn làm thêm giờ.");

        var warnings = new List<string> { "Đơn sẽ được chuyển Quản lý duyệt; giờ OT chỉ được tính vào bảng công khi đơn được duyệt." };
        var existing = await _hr.GetMyRequestsAsync(userId, null, 50, ct);
        foreach (var e in existing.Where(e => e.Category == RequestKind.Overtime && e.Status is "pending" or "approved" && e.StartDate == req.Date))
            warnings.Add($"Bạn đã có đơn OT #{e.Id} ({StatusVi(e.Status)}) trong ngày {Vi.Date(req.Date)} — nếu trùng giờ, hệ thống sẽ từ chối khi gửi.");
        if (type.RequiresAttachment)
            warnings.Add("Loại đơn này bắt buộc kèm ảnh minh chứng — hãy đính kèm khi xác nhận.");

        var hours = RequestRules.OvertimeHours(startAt, endAt);
        var lines = new List<DraftLine>
        {
            new("Loại đơn", type.Name),
            new("Ngày", Vi.DateWithWeekday(req.Date)),
            new("Giờ làm thêm", $"{Vi.Time(req.StartTime.ToTimeSpan())} – {Vi.Time(req.EndTime.ToTimeSpan())}{(endAt.Date > startAt.Date ? " (qua nửa đêm)" : "")}"),
            new("Số giờ", $"{Vi.Num(hours)} giờ"),
            new("Lý do", reason)
        };

        var draft = NewDraft(
            userId, conversationId,
            new DraftCard("overtime", "Đơn làm thêm giờ — bản nháp", lines, warnings, type.RequiresAttachment),
            new LeaveRequestSubmission(userId, type.Id, req.Date, DateOnly.FromDateTime(endAt), 0m, reason, null, null, null, startAt, endAt));

        await _drafts.SaveAsync(draft, ct);
        return DraftBuildResult.Success(draft);
    }

    // ------------------------------------------------------------------------------------------------
    // Nháp đơn giải trình đi muộn / về sớm / chờ duyệt
    // ------------------------------------------------------------------------------------------------
    public async Task<DraftBuildResult> BuildExplanationDraftAsync(
        int userId, ExplanationDraftRequest req, long? conversationId, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var reason = (req.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return DraftBuildResult.Fail("missing_reason", "Chưa có lý do giải trình. Hãy hỏi người dùng lý do rồi gọi lại.");
        if (reason.Length > 500) reason = reason[..500];

        if (req.Date > today)
            return DraftBuildResult.Fail("future_date", "Chỉ giải trình được lượt chấm công đã diễn ra, không phải ngày trong tương lai.");
        if (req.Date < today.AddDays(-RequestRules.MaxBackDays))
            return DraftBuildResult.Fail("too_old", $"Chỉ giải trình được lượt chấm công trong vòng {RequestRules.MaxBackDays} ngày gần đây.");

        var types = await _hr.GetLeaveTypesAsync(ct);
        var type = types.FirstOrDefault(t => t.Category == RequestKind.Explanation);
        if (type is null)
            return DraftBuildResult.Fail("explanation_not_supported", "Hệ thống chưa cấu hình loại đơn giải trình.");

        var logs = (await _hr.GetMyAttendanceLogsAsync(userId, req.Date, req.Date, ct)).OrderBy(l => l.CheckinTime).ToList();
        if (logs.Count == 0)
            return DraftBuildResult.Fail("no_attendance_log", $"Bạn không có lượt chấm công nào trong {Vi.DateWithWeekday(req.Date)}.");

        var needing = logs.Where(l => l.NeedsExplanation).ToList();
        if (needing.Count == 0)
            return DraftBuildResult.Fail("nothing_to_explain", $"Các lượt chấm công ngày {Vi.Date(req.Date)} đều đúng giờ và hợp lệ, không cần giải trình.");

        if (req.Part is { } part)
        {
            var byPart = needing.Where(l => l.ShiftStartTime is { } s && ShiftPartClassifier.Classify(s) == part).ToList();
            if (byPart.Count > 0) needing = byPart;
        }

        if (needing.Count > 1)
            return DraftBuildResult.Fail("ambiguous_log", "Ngày đó có nhiều lượt cần giải trình, hỏi người dùng muốn giải trình ca nào.",
                new JsonObject { ["logs"] = new JsonArray(needing.Select(l => (JsonNode)LogJson(l)).ToArray()) });
        var log = needing[0];

        var warnings = new List<string> { "Đơn sẽ được chuyển Quản lý duyệt; nếu được duyệt thì lượt này không bị tính vi phạm." };
        var existing = await _hr.GetMyRequestsAsync(userId, null, 50, ct);
        foreach (var e in existing.Where(e => e.Category == RequestKind.Explanation && e.Status is "pending" or "approved" && e.StartDate == log.Date))
            warnings.Add($"Bạn đã có đơn giải trình #{e.Id} ({StatusVi(e.Status)}) cho ngày {Vi.Date(log.Date)} — gửi thêm có thể bị từ chối.");
        if (type.RequiresAttachment)
            warnings.Add("Loại đơn này bắt buộc kèm ảnh minh chứng — hãy đính kèm khi xác nhận.");

        var lines = new List<DraftLine>
        {
            new("Loại đơn", type.Name),
            new("Ca được giải trình", $"{Vi.DateWithWeekday(log.Date)} · {log.ShiftName}"),
            new("Vấn đề", LogIssueText(log)),
            new("Lý do", reason)
        };

        var draft = NewDraft(
            userId, conversationId,
            new DraftCard("explanation", "Đơn giải trình — bản nháp", lines, warnings, type.RequiresAttachment),
            new LeaveRequestSubmission(userId, type.Id, log.Date, log.Date, 0m, reason, null, null, null, AttendanceLogId: log.Id));

        await _drafts.SaveAsync(draft, ct);
        return DraftBuildResult.Success(draft);
    }

    // ------------------------------------------------------------------------------------------------
    // Xác nhận / hủy
    // ------------------------------------------------------------------------------------------------
    public async Task<(AssistantDraft Draft, SubmissionResult Result)> ConfirmAsync(
        Guid draftId, int userId, SubmissionAttachment? attachment, CancellationToken ct = default)
    {
        var draft = await GetOwnedAsync(draftId, userId, ct);

        if (draft.Status == DraftStatus.Confirmed && draft.RequestId is { } existing)
            return (draft, new SubmissionResult(existing)); // bấm lặp → trả kết quả cũ, không gửi lại
        if (draft.Status == DraftStatus.Cancelled)
            throw new BadRequestException("Bản nháp này đã bị hủy.");
        if (draft.Status == DraftStatus.Expired || _clock.Now > draft.ExpiresAt)
        {
            await _drafts.TryTransitionAsync(draft.Id, DraftStatus.Pending, DraftStatus.Expired, ct);
            throw new BadRequestException("Bản nháp đã hết hạn, hãy nhờ trợ lý tạo lại.");
        }

        if (draft.Card.RequiresAttachment && attachment is null)
            throw new BadRequestException("Loại đơn này cần đính kèm minh chứng trước khi gửi.");

        if (!await _drafts.TryTransitionAsync(draft.Id, DraftStatus.Pending, DraftStatus.Confirmed, ct))
            throw new BadRequestException("Bản nháp đang được xử lý hoặc đã đóng.");

        try
        {
            var result = await _submitter.SubmitAsync(draft.Submission, attachment, ct);
            await _drafts.SetRequestIdAsync(draft.Id, result.RequestId, ct);
            draft.Status = DraftStatus.Confirmed;
            draft.RequestId = result.RequestId;
            return (draft, result);
        }
        catch
        {
            // Gửi lỗi → trả bản nháp về trạng thái chờ để người dùng bấm lại được
            await _drafts.TryTransitionAsync(draft.Id, DraftStatus.Confirmed, DraftStatus.Pending, CancellationToken.None);
            throw;
        }
    }

    public async Task<AssistantDraft> CancelAsync(Guid draftId, int userId, CancellationToken ct = default)
    {
        var draft = await GetOwnedAsync(draftId, userId, ct);
        if (draft.Status == DraftStatus.Confirmed)
            throw new BadRequestException("Đơn đã được gửi, không hủy bản nháp được nữa.");
        if (draft.Status == DraftStatus.Pending)
        {
            await _drafts.TryTransitionAsync(draft.Id, DraftStatus.Pending, DraftStatus.Cancelled, ct);
            draft.Status = DraftStatus.Cancelled;
        }
        return draft;
    }

    public Task<AssistantDraft> GetAsync(Guid draftId, int userId, CancellationToken ct = default) =>
        GetOwnedAsync(draftId, userId, ct);

    private async Task<AssistantDraft> GetOwnedAsync(Guid draftId, int userId, CancellationToken ct)
    {
        var draft = await _drafts.GetAsync(draftId, ct);
        // Của người khác cũng báo "không tìm thấy" để không lộ sự tồn tại của bản nháp
        if (draft is null || draft.UserId != userId)
            throw new NotFoundException("Không tìm thấy bản nháp.");
        return draft;
    }

    // ------------------------------------------------------------------------------------------------
    private AssistantDraft NewDraft(int userId, long? conversationId, DraftCard card, LeaveRequestSubmission submission)
    {
        var now = _clock.Now;
        return new AssistantDraft
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConversationId = conversationId,
            Card = card,
            Submission = submission,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.DraftTtlMinutes)
        };
    }

    private static string RangeText(DateOnly start, DateOnly end) =>
        start == end ? Vi.DateWithWeekday(start) : $"{Vi.DateWithWeekday(start)} → {Vi.DateWithWeekday(end)}";

    private static string StatusVi(string status) => status switch
    {
        "pending" => "đang chờ duyệt",
        "approved" => "đã duyệt",
        "rejected" => "bị từ chối",
        "cancelled" => "đã hủy",
        _ => status
    };

    private static string LogIssueText(AttendanceLogDto l)
    {
        var parts = new List<string>();
        if (l.LateMinutes > 0) parts.Add($"đi muộn {l.LateMinutes} phút");
        if (l.EarlyMinutes > 0) parts.Add($"về sớm {l.EarlyMinutes} phút");
        if (l.Status == AttendanceStatus.PendingApproval) parts.Add("lượt chấm công đang chờ duyệt");
        return string.Join(", ", parts);
    }

    private static JsonObject LogJson(AttendanceLogDto l) => new()
    {
        ["date"] = l.Date.ToString("yyyy-MM-dd"),
        ["shift"] = l.ShiftName,
        ["issue"] = LogIssueText(l)
    };

    private static string ShiftText(ScheduledShiftDto s) => $"{s.ShiftName} ({Vi.Time(s.StartTime)}–{Vi.Time(s.EndTime)})";

    private static JsonObject ShiftJson(ScheduledShiftDto s) => new()
    {
        ["date"] = s.Date.ToString("yyyy-MM-dd"),
        ["shift"] = s.ShiftName,
        ["part"] = ShiftPartClassifier.Label(ShiftPartClassifier.Classify(s.StartTime)),
        ["start"] = Vi.Time(s.StartTime),
        ["end"] = Vi.Time(s.EndTime)
    };
}
