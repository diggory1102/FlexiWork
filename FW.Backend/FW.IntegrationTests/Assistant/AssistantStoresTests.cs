using FW.Application.Common.Interfaces;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.Assistant;

/// <summary>
/// Kho lưu trữ Trợ lý AI / cảnh báo nghỉ việc (migration 018) và nguồn dữ liệu chấm điểm trên MySQL thật.
/// Dữ liệu nằm ở năm 2038 để không lẫn với test khác; mỗi test tự tạo nhân viên riêng.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AssistantStoresTests
{
    private const int Branch1 = 1;
    private static readonly DateTime Now = new(2038, 3, 11, 10, 0, 0); // Thứ Năm

    private readonly DatabaseFixture _db;

    public AssistantStoresTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => AssistantStoresTests.Now;
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private static async Task<User> NewEmployeeAsync(DbSession s, string role = RoleNames.Employee, int branchId = Branch1)
    {
        var code = $"AI{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        var roleEntity = (await new RoleRepository(s).GetByNameAsync(role))!;
        await new UserRepository(s).SetRolesAsync(user.Id, new[] { roleEntity.Id });
        return user;
    }

    // ------------------------------------------------------------------ hội thoại

    [SkippableFact]
    public async Task Conversations_are_private_to_their_owner_and_messages_keep_order()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var me = await NewEmployeeAsync(s);
        var other = await NewEmployeeAsync(s);
        var store = new AssistantStore(s);

        var conversation = await store.CreateConversationAsync(me.Id, new string('x', 300), Now);
        Assert.Equal(200, conversation.Title.Length); // cột title tối đa 200

        var draftId = Guid.NewGuid();
        await store.AddMessageAsync(conversation.Id, "user", "Em còn mấy ngày phép?", null, Array.Empty<Guid>(), Now);
        await store.AddMessageAsync(conversation.Id, "assistant", "Bạn còn 11 ngày.", "[{\"tool\":\"get_my_leave_balance\"}]", new[] { draftId }, Now.AddSeconds(5));

        var messages = await store.GetRecentMessagesAsync(conversation.Id, 10);
        Assert.Equal(new[] { "user", "assistant" }, messages.Select(m => m.Role));
        Assert.Equal(new[] { draftId }, messages[1].DraftIds);
        Assert.Empty(messages[0].DraftIds);
        Assert.Contains("get_my_leave_balance", messages[1].ToolTrace);

        Assert.NotNull(await store.GetConversationAsync(conversation.Id, me.Id));
        Assert.Null(await store.GetConversationAsync(conversation.Id, other.Id)); // người khác không thấy
        Assert.Empty(await store.ListConversationsAsync(other.Id, 10));

        // Tin mới nhất khi bị giới hạn số lượng vẫn là tin cuối
        var last = await store.GetRecentMessagesAsync(conversation.Id, 1);
        Assert.Equal("assistant", Assert.Single(last).Role);
    }

    [SkippableFact]
    public async Task Daily_question_count_only_counts_my_user_messages_on_that_day()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var me = await NewEmployeeAsync(s);
        var other = await NewEmployeeAsync(s);
        var store = new AssistantStore(s);

        var mine = await store.CreateConversationAsync(me.Id, "a", Now);
        var theirs = await store.CreateConversationAsync(other.Id, "b", Now);
        await store.AddMessageAsync(mine.Id, "user", "1", null, Array.Empty<Guid>(), Now);
        await store.AddMessageAsync(mine.Id, "assistant", "r", null, Array.Empty<Guid>(), Now);       // trả lời không tính
        await store.AddMessageAsync(mine.Id, "user", "2", null, Array.Empty<Guid>(), Now.AddHours(5));
        await store.AddMessageAsync(mine.Id, "user", "hôm qua", null, Array.Empty<Guid>(), Now.AddDays(-1));
        await store.AddMessageAsync(theirs.Id, "user", "x", null, Array.Empty<Guid>(), Now);

        Assert.Equal(2, await store.CountUserMessagesOnAsync(me.Id, DateOnly.FromDateTime(Now)));
        Assert.Equal(1, await store.CountUserMessagesOnAsync(me.Id, DateOnly.FromDateTime(Now.AddDays(-1))));
        Assert.Equal(1, await store.CountUserMessagesOnAsync(other.Id, DateOnly.FromDateTime(Now)));
    }

    // ------------------------------------------------------------------ bản nháp

    private static AssistantDraft NewDraft(int userId, long? conversationId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ConversationId = conversationId,
        Card = new DraftCard("overtime", "Đơn làm thêm giờ — bản nháp",
            new[] { new DraftLine("Ngày", "Thứ Sáu 12/03/2038"), new DraftLine("Lý do", "kiểm kho — có \"dấu nháy\" & tiếng Việt") },
            new[] { "Cảnh báo thử" }, RequiresAttachment: true),
        Submission = new LeaveRequestSubmission(userId, 5, new DateOnly(2038, 3, 12), new DateOnly(2038, 3, 13), 0m, "kiểm kho", null, null, null,
            new DateTime(2038, 3, 12, 22, 0, 0), new DateTime(2038, 3, 13, 1, 0, 0), 77),
        CreatedAt = Now,
        ExpiresAt = Now.AddMinutes(30)
    };

    [SkippableFact]
    public async Task Draft_round_trips_card_and_submission_exactly()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var me = await NewEmployeeAsync(s);
        var conversation = await new AssistantStore(s).CreateConversationAsync(me.Id, "t", Now);
        var store = new DraftStore(s);
        var draft = NewDraft(me.Id, conversation.Id);

        await store.SaveAsync(draft);
        var loaded = (await store.GetAsync(draft.Id))!;

        Assert.Equal(draft.Id, loaded.Id);
        Assert.Equal(me.Id, loaded.UserId);
        Assert.Equal(conversation.Id, loaded.ConversationId);
        Assert.Equal(DraftStatus.Pending, loaded.Status);
        Assert.Equal(draft.Card.Title, loaded.Card.Title);
        Assert.Equal("kiểm kho — có \"dấu nháy\" & tiếng Việt", loaded.Card.Lines[1].Value);
        Assert.True(loaded.Card.RequiresAttachment);
        Assert.Equal(draft.Submission, loaded.Submission); // gồm StartAt/EndAt/AttendanceLogId
        Assert.Equal(Now.AddMinutes(30), loaded.ExpiresAt);
        Assert.Null(await store.GetAsync(Guid.NewGuid()));
    }

    [SkippableFact]
    public async Task Draft_transition_is_atomic_so_only_one_confirm_wins()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var me = await using_user();
        var draft = NewDraft(me, null);
        await using (var s = _db.CreateSession()) await new DraftStore(s).SaveAsync(draft);

        // Tám lần bấm "Xác nhận" đồng thời, mỗi lần một phiên (một request) riêng
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var s = _db.CreateSession();
            return await new DraftStore(s).TryTransitionAsync(draft.Id, DraftStatus.Pending, DraftStatus.Confirmed);
        }));
        Assert.Equal(1, results.Count(r => r));

        await using var check = _db.CreateSession();
        var store = new DraftStore(check);
        Assert.Equal(DraftStatus.Confirmed, (await store.GetAsync(draft.Id))!.Status);
        Assert.False(await store.TryTransitionAsync(draft.Id, DraftStatus.Pending, DraftStatus.Cancelled)); // đã đóng

        // Gửi lỗi → trả về chờ rồi ghi mã đơn khi gửi lại thành công
        Assert.True(await store.TryTransitionAsync(draft.Id, DraftStatus.Confirmed, DraftStatus.Pending));
        await store.SetRequestIdAsync(draft.Id, 4242);
        Assert.Equal(4242, (await store.GetAsync(draft.Id))!.RequestId);

        async Task<int> using_user()
        {
            await using var s = _db.CreateSession();
            return (await NewEmployeeAsync(s)).Id;
        }
    }

    // ------------------------------------------------------------------ điểm nguy cơ

    private static AttritionSnapshot Snap(User u, DateOnly week, int score, RiskLevel level, int? previous = null, bool sufficient = true) => new(
        u.Id, u.FullName, u.BranchId, week, score, level, sufficient, previous,
        new[] { "Đi muộn nhiều hơn thường lệ (có \"dấu nháy\")" },
        new[] { new SignalResult("late_frequency", "Tần suất đi muộn", 0.6, 0.1, 0.9, 0.15, 13.5, "Đi muộn tăng") },
        Now);

    [SkippableFact]
    public async Task Snapshots_upsert_per_week_and_latest_picks_the_newest_week_per_employee()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s, branchId: 2);
        var store = new AttritionSnapshotStore(s);
        var w1 = new DateOnly(2038, 3, 1);
        var w2 = new DateOnly(2038, 3, 8);

        await store.UpsertAsync(new[] { Snap(a, w1, 30, RiskLevel.Medium), Snap(b, w1, 10, RiskLevel.Low) });
        await store.UpsertAsync(new[] { Snap(a, w2, 55, RiskLevel.High, previous: 30) });
        await store.UpsertAsync(new[] { Snap(a, w2, 60, RiskLevel.High, previous: 30) }); // chạy lại cùng tuần → ghi đè, không trùng

        var history = await store.GetHistoryAsync(a.Id, 12);
        Assert.Equal(new[] { w1, w2 }, history.Select(h => h.WeekStart));   // cũ → mới
        Assert.Equal(60, history[1].Score);
        Assert.Equal(30, history[1].PreviousScore);
        Assert.Equal(30, history[1].Delta);
        Assert.Equal("Đi muộn nhiều hơn thường lệ (có \"dấu nháy\")", history[1].Reasons.Single());
        Assert.Equal("late_frequency", history[1].Signals.Single().Key);
        Assert.Equal(RiskLevel.High, history[1].Level);

        var latest = await store.GetLatestAsync(null);
        Assert.Equal(w2, latest.Single(x => x.UserId == a.Id).WeekStart);
        Assert.Equal(w1, latest.Single(x => x.UserId == b.Id).WeekStart);
        Assert.DoesNotContain(await store.GetLatestAsync(2), x => x.UserId == a.Id);       // lọc chi nhánh
        Assert.Contains(await store.GetLatestAsync(2), x => x.UserId == b.Id);

        var limited = await store.GetHistoryAsync(a.Id, 1);
        Assert.Equal(w2, Assert.Single(limited).WeekStart);                                // chỉ tuần mới nhất
    }

    // ------------------------------------------------------------------ nguồn dữ liệu chấm điểm

    private static async Task<Shift> NewShiftAsync(DbSession s, int startHour, int endHour)
    {
        var shift = new Shift
        {
            ShiftCode = $"AI{Guid.NewGuid():N}"[..14], Name = $"Ca {startHour}h-{endHour}h", BranchId = Branch1,
            StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour)
        };
        await new ShiftRepository(s).AddAsync(shift);
        return shift;
    }

    private static async Task<Schedule> NewScheduleAsync(DbSession s, User user, Shift shift, DateTime day, string status = ScheduleStatus.Scheduled)
    {
        var schedule = new Schedule
        {
            UserId = user.Id, ShiftId = shift.Id, BranchId = user.BranchId ?? Branch1, WorkDate = day, Status = status, IsPublished = true, PublishedAt = day.AddDays(-5)
        };
        await new ScheduleRepository(s).AddAsync(schedule);
        return schedule;
    }

    private static Task NewLogAsync(DbSession s, User user, Schedule schedule, int startHour, string status = AttendanceStatus.Valid,
        int lateMinutes = 0, int earlyMinutes = 0, DateTime? checkout = null)
    {
        var day = schedule.WorkDate.Date;
        return new AttendanceLogRepository(s).AddAsync(new AttendanceLog
        {
            ScheduleId = schedule.Id, UserId = user.Id, BranchId = schedule.BranchId, WorkDate = day,
            CheckinTime = day.AddHours(startHour).AddMinutes(lateMinutes), CheckinImageUrl = "https://img.test/in.jpg", CheckinLat = 21.0316m, CheckinLng = 105.7831m,
            CheckinDistance = 10, CheckinWifiMatched = true, CheckinFaceMatched = true, LateMinutes = lateMinutes, EarlyMinutes = earlyMinutes,
            CheckoutTime = checkout, ActualWorkHours = checkout.HasValue ? 4 : 0, Status = status
        });
    }

    private static async Task<int> AddRequestAsync(DbSession s, User user, string category, DateTime createdAt, string status = RequestStatus.Pending, decimal days = 1)
    {
        var types = await new LeaveTypeRepository(s).GetListAsync(activeOnly: true);
        var type = types.First(t => t.Category == category);
        var request = new LeaveRequest
        {
            UserId = user.Id, BranchId = user.BranchId, LeaveTypeId = type.Id, StartDate = createdAt.Date, EndDate = createdAt.Date, DurationDays = days,
            StartAt = category == RequestCategory.Overtime ? createdAt.Date.AddHours(18) : null,
            EndAt = category == RequestCategory.Overtime ? createdAt.Date.AddHours(20) : null,
            Reason = "thử nghiệm", Status = status
        };
        var id = await new LeaveRequestRepository(s).AddAsync(request);
        // created_at do cơ sở dữ liệu đặt — ghi đè để rơi vào cửa sổ cần thử
        await s.ExecuteInTransactionAsync(async ct =>
        {
            var connection = await s.GetConnectionAsync(ct);
            await Dapper.SqlMapper.ExecuteAsync(connection, "UPDATE leave_requests SET created_at = @at WHERE id = @id", new { at = createdAt, id }, s.Transaction);
        });
        return id;
    }

    [SkippableFact]
    public async Task Data_source_derives_absence_and_attendance_from_the_published_schedule()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var emp = await NewEmployeeAsync(s);
        var manager = await NewEmployeeAsync(s, RoleNames.Manager);   // không phải nhân viên → không chấm điểm
        var morning = await NewShiftAsync(s, 8, 12);
        var day = (int offset) => new DateTime(2038, 3, 8).AddDays(offset); // Thứ Hai 08/03 → ...

        // Hôm nay là Thứ Năm 11/03 10:00 — các ca các ngày trước đã kết thúc
        var onTime = await NewScheduleAsync(s, emp, morning, day(0));
        await NewLogAsync(s, emp, onTime, 8, checkout: day(0).AddHours(12));
        var late = await NewScheduleAsync(s, emp, morning, day(1));
        await NewLogAsync(s, emp, late, 8, lateMinutes: 15, earlyMinutes: 20, checkout: day(1).AddHours(11).AddMinutes(40));
        await NewScheduleAsync(s, emp, morning, day(2));                                                // không chấm công → vắng mặt
        await NewScheduleAsync(s, emp, morning, day(-10));                                              // ngoài cửa sổ
        var onLeave = await NewScheduleAsync(s, emp, await NewShiftAsync(s, 13, 17), day(0), ScheduleStatus.OnLeave); // nghỉ phép → bỏ qua
        var rejected = await NewScheduleAsync(s, emp, await NewShiftAsync(s, 18, 22), day(0));
        await NewLogAsync(s, emp, rejected, 18, AttendanceStatus.Invalid);                              // bị từ chối → bỏ qua
        await NewScheduleAsync(s, emp, morning, day(3));                                                // hôm nay, ca đang diễn ra → chưa đủ căn cứ
        await NewScheduleAsync(s, manager, morning, day(1));

        await AddRequestAsync(s, emp, RequestCategory.Leave, day(0).AddHours(9), days: 2);
        await AddRequestAsync(s, emp, RequestCategory.ShiftSwap, day(1).AddHours(9), days: 0);
        await AddRequestAsync(s, emp, RequestCategory.Overtime, day(1).AddHours(10), days: 0);
        await AddRequestAsync(s, emp, RequestCategory.Leave, day(2).AddHours(9), RequestStatus.Rejected);   // bị từ chối → bỏ qua
        await AddRequestAsync(s, emp, RequestCategory.Leave, day(-20).AddHours(9));                         // ngoài cửa sổ

        var source = new AttritionDataSource(s, new ReportRepository(s), new FixedClock());
        var all = await source.GetActivitiesAsync(new DateOnly(2038, 3, 1), new DateOnly(2038, 3, 15));

        Assert.DoesNotContain(all, a => a.UserId == manager.Id);
        var mine = all.Single(a => a.UserId == emp.Id);
        Assert.Equal(emp.FullName, mine.Name);
        Assert.Equal(Branch1, mine.BranchId);

        var shifts = mine.Shifts.OrderBy(x => x.Date).ToList();
        Assert.Equal(new[] { new DateOnly(2038, 3, 8), new DateOnly(2038, 3, 9), new DateOnly(2038, 3, 10) }, shifts.Select(x => x.Date));
        Assert.Equal((true, false, 0, 0), (shifts[0].Attended, shifts[0].Absent, shifts[0].LateMinutes, shifts[0].EarlyMinutes));
        Assert.Equal((true, false, 15, 20), (shifts[1].Attended, shifts[1].Absent, shifts[1].LateMinutes, shifts[1].EarlyMinutes));
        Assert.Equal((false, true), (shifts[2].Attended, shifts[2].Absent));

        var requests = mine.Requests.OrderBy(r => r.SubmittedOn).ThenBy(r => r.Category).ToList();
        Assert.Equal(3, requests.Count);
        Assert.Contains(requests, r => r.Category == RequestKind.Leave && r.Days == 2m && r.SubmittedOn == new DateOnly(2038, 3, 8));
        Assert.Contains(requests, r => r.Category == RequestKind.ShiftSwap);
        Assert.Contains(requests, r => r.Category == RequestKind.Overtime);
        Assert.Null(mine.Availability);
    }

    [SkippableFact]
    public async Task Approved_explanation_exempts_a_late_arrival_from_the_attrition_data()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var emp = await NewEmployeeAsync(s);
        var morning = await NewShiftAsync(s, 8, 12);
        var schedule = await NewScheduleAsync(s, emp, morning, new DateTime(2038, 3, 9));
        await NewLogAsync(s, emp, schedule, 8, lateMinutes: 25, checkout: new DateTime(2038, 3, 9, 12, 0, 0));
        var log = (await new AttendanceLogRepository(s).GetByScheduleIdAsync(schedule.Id))!;

        var types = await new LeaveTypeRepository(s).GetListAsync(activeOnly: true);
        await new LeaveRequestRepository(s).AddAsync(new LeaveRequest
        {
            UserId = emp.Id, BranchId = emp.BranchId, LeaveTypeId = types.First(t => t.Category == RequestCategory.Explanation).Id,
            StartDate = log.WorkDate, EndDate = log.WorkDate, AttendanceLogId = log.Id, Reason = "kẹt xe", Status = RequestStatus.Approved
        });

        var source = new AttritionDataSource(s, new ReportRepository(s), new FixedClock());
        var mine = (await source.GetActivitiesAsync(new DateOnly(2038, 3, 1), new DateOnly(2038, 3, 15))).Single(a => a.UserId == emp.Id);

        var shift = Assert.Single(mine.Shifts);
        Assert.True(shift.Attended);
        Assert.Equal(0, shift.LateMinutes); // đã giải trình và được duyệt
    }
}
