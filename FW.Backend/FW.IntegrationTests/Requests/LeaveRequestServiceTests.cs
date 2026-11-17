using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Requests;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Requests;

/// <summary>
/// Đơn từ (Task 4.1) trên MySQL thật: loại đơn, đơn nghỉ (quỹ phép, trùng ngày, ảnh bắt buộc), OT (trùng ca, trùng OT),
/// đổi ca (đồng nghiệp đồng ý / từ chối, chồng giờ), giải trình, rút đơn, phạm vi xem. Đồng hồ cố định ở tháng 3/2034 để không
/// đụng dữ liệu khác. Seed: loại đơn 1 = phép năm (trừ phép), 2 = nghỉ ốm (bắt buộc ảnh), 4 = đổi ca, 5 = OT, nhóm giải trình;
/// người dùng 2 = Quản lý chi nhánh 1, 4 = HR; system_settings.annual_leave_days = 12.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class LeaveRequestServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, ManagerId = 2, HrId = 4;
    private const int AnnualLeave = 1, SickLeave = 2, Swap = 4, Overtime = 5;
    private static readonly DateTime Now = new(2034, 3, 10, 10, 0, 0);
    private static readonly DateTime Day = new(2034, 3, 12);

    private readonly DatabaseFixture _db;

    public LeaveRequestServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ test doubles

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => LeaveRequestServiceTests.Now;
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private sealed class TestUser : ICurrentUserService
    {
        private readonly string[] _roles;

        public TestUser(int userId, int? branchId, params string[] roles)
        {
            UserId = userId;
            BranchId = branchId;
            _roles = roles.Length == 0 ? new[] { RoleNames.Employee } : roles;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => _roles.Contains(role);
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        public List<string> Uploaded { get; } = new();

        public Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            var id = $"{folder}/{Guid.NewGuid():N}";
            Uploaded.Add(id);
            return Task.FromResult(new FileUploadResult($"https://img.test/{id}.jpg", id));
        }

        public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private static LeaveRequestService Service(DbSession s, ICurrentUserService user, MemoryStorage? storage = null) => new(
        new LeaveRequestRepository(s), new LeaveTypeRepository(s), new LeaveBalanceRepository(s), new ScheduleRepository(s),
        new ShiftRepository(s), new AttendanceLogRepository(s), new UserRepository(s), new SystemSettingRepository(s),
        storage ?? new MemoryStorage(), user, new FixedClock(), NullLogger<LeaveRequestService>.Instance);

    private static LeaveRequestService As(DbSession s, User user, MemoryStorage? storage = null) =>
        Service(s, new TestUser(user.Id, user.BranchId), storage);

    // ------------------------------------------------------------------ dữ liệu

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId = Branch1)
    {
        var code = $"R{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static async Task<Shift> NewShiftAsync(DbSession s, int startHour, int endHour)
    {
        var shift = new Shift
        {
            ShiftCode = $"RQ{Guid.NewGuid():N}"[..14], Name = $"Ca {startHour}h-{endHour}h", BranchId = Branch1,
            StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour),
            IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour))
        };
        await new ShiftRepository(s).AddAsync(shift);
        return shift;
    }

    private static async Task<Schedule> NewScheduleAsync(DbSession s, User user, Shift shift, DateTime day, bool published = true)
    {
        var schedule = new Schedule
        {
            UserId = user.Id, ShiftId = shift.Id, BranchId = Branch1, WorkDate = day, IsPublished = published,
            PublishedAt = published ? Now.AddDays(-5) : null
        };
        await new ScheduleRepository(s).AddAsync(schedule);
        return schedule;
    }

    private static CreateLeaveRequestForm Leave(int typeId, DateTime from, DateTime to) => new()
    {
        LeaveTypeId = typeId, Reason = "Về quê có việc gia đình", StartDate = DateOnly.FromDateTime(from), EndDate = DateOnly.FromDateTime(to)
    };

    private static RequestAttachment Photo() => new(new MemoryStream(new byte[] { 1, 2, 3 }), "giay-kham.jpg", "image/jpeg", 3);

    // ------------------------------------------------------------------ Loại đơn

    [SkippableFact]
    public async Task Leave_types_are_validated_and_cannot_lose_their_requests()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var types = new LeaveTypeService(new LeaveTypeRepository(s));
        var name = $"Nghỉ cưới {Guid.NewGuid():N}"[..24];

        var created = await types.CreateAsync(new LeaveTypeRequest { Name = name, Category = RequestCategory.Leave, IsPaid = true });
        Assert.True(created.IsActive);
        await Assert.ThrowsAsync<BadRequestException>(() => types.CreateAsync(new LeaveTypeRequest { Name = name, Category = RequestCategory.Leave }));
        await Assert.ThrowsAsync<BadRequestException>(() => types.CreateAsync(
            new LeaveTypeRequest { Name = name + "x", Category = RequestCategory.Overtime, DeductsBalance = true }));
        await Assert.ThrowsAsync<BadRequestException>(() => types.CreateAsync(new LeaveTypeRequest { Name = name + "y", Category = "late" }));

        // Có đơn đang chờ → không đổi nhóm, không xóa được; tạm ngừng thì biến khỏi danh sách tạo đơn
        var employee = await NewEmployeeAsync(s);
        var request = await As(s, employee).CreateAsync(Leave(created.Id, Day, Day), null);
        await Assert.ThrowsAsync<BadRequestException>(() => types.UpdateAsync(created.Id,
            new LeaveTypeRequest { Name = name, Category = RequestCategory.Overtime }));
        await Assert.ThrowsAsync<BadRequestException>(() => types.DeleteAsync(created.Id));

        await types.UpdateAsync(created.Id, new LeaveTypeRequest { Name = name, Category = RequestCategory.Leave, IsActive = false });
        Assert.DoesNotContain(await types.GetListAsync(activeOnly: true), t => t.Id == created.Id);
        Assert.Contains(await types.GetListAsync(activeOnly: false), t => t.Id == created.Id);
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, employee).CreateAsync(Leave(created.Id, Day.AddDays(3), Day.AddDays(3)), null));

        await As(s, employee).CancelAsync(request.Id);
        await types.DeleteAsync(created.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => types.GetByIdAsync(created.Id));
    }

    // ------------------------------------------------------------------ Nghỉ

    [SkippableFact]
    public async Task Leave_requests_check_overlap_balance_and_required_attachment()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var storage = new MemoryStorage();
        var service = As(s, employee, storage);

        var first = await service.CreateAsync(Leave(AnnualLeave, Day, Day.AddDays(2)), null);
        Assert.Equal(RequestStatus.Pending, first.Status);
        Assert.Equal(3, first.DurationDays);
        Assert.Equal(RequestCategory.Leave, first.Category);
        Assert.Equal(Branch1, first.BranchId);
        Assert.True(first.CanCancel);

        // Trùng ngày với đơn đang chờ
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(Leave(SickLeave, Day.AddDays(2), Day.AddDays(4)), Photo()));

        // Quỹ phép mặc định 12 ngày, 3 ngày đang chờ duyệt → còn xin được 9
        var balance = await service.GetMyBalanceAsync(null);
        Assert.Equal((2034, 12m, 3m, 9m, true), (balance.Year, balance.TotalDays, balance.PendingDays, balance.AvailableDays, balance.IsDefault));
        var tooLong = await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(Leave(AnnualLeave, Day.AddDays(10), Day.AddDays(19)), null));
        Assert.Contains("chỉ còn 9 ngày", tooLong.Message);

        // Nghỉ ốm bắt buộc ảnh minh chứng
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(Leave(SickLeave, Day.AddDays(5), Day.AddDays(5)), null));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(Leave(SickLeave, Day.AddDays(5), Day.AddDays(5)),
            new RequestAttachment(new MemoryStream(new byte[] { 1 }), "a.pdf", "application/pdf", 1)));
        var sick = await service.CreateAsync(Leave(SickLeave, Day.AddDays(5), Day.AddDays(5)), Photo());
        Assert.StartsWith("https://img.test/requests/", sick.AttachmentUrl);
        Assert.Single(storage.Uploaded);

        // Người khác không rút được; người tạo rút → quỹ phép không còn bị giữ chỗ
        var other = await NewEmployeeAsync(s);
        await Assert.ThrowsAsync<ForbiddenException>(() => As(s, other).CancelAsync(first.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => As(s, other).GetByIdAsync(first.Id));
        var cancelled = await service.CancelAsync(first.Id);
        Assert.Equal(RequestStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.CanCancel);
        await Assert.ThrowsAsync<BadRequestException>(() => service.CancelAsync(first.Id));
        Assert.Equal(12m, (await service.GetMyBalanceAsync(2034)).AvailableDays);

        var mine = await service.GetMineAsync(new LeaveRequestFilter { Category = RequestCategory.Leave });
        Assert.Equal(new[] { sick.Id, first.Id }, mine.Items.Select(i => i.Id));
        Assert.Single((await service.GetMineAsync(new LeaveRequestFilter { Status = RequestStatus.Cancelled })).Items);
    }

    // ------------------------------------------------------------------ OT

    [SkippableFact]
    public async Task Overtime_must_be_outside_scheduled_shifts_and_not_overlap_other_overtime()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        await NewScheduleAsync(s, employee, await NewShiftAsync(s, 8, 17), Day);
        var service = As(s, employee);
        CreateLeaveRequestForm Ot(int fromHour, int fromMinute, int toHour, int toMinute) => new()
        {
            LeaveTypeId = Overtime, Reason = "Kiểm kho cuối tháng",
            StartAt = Day.AddHours(fromHour).AddMinutes(fromMinute), EndAt = Day.AddHours(toHour).AddMinutes(toMinute)
        };

        var clash = await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(Ot(16, 0, 19, 0), null));
        Assert.Contains("trùng với ca", clash.Message);

        var ot = await service.CreateAsync(Ot(17, 0, 19, 30), null); // nối tiếp ngay sau ca
        Assert.Equal(2.5m, ot.DurationHours);
        Assert.Equal(Day.AddHours(17), ot.StartAt);
        Assert.Equal(0, ot.DurationDays);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(Ot(19, 0, 21, 0), null));
        Assert.Equal(RequestStatus.Pending, (await service.CreateAsync(Ot(19, 30, 21, 0), null)).Status);
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(new CreateLeaveRequestForm { LeaveTypeId = Overtime, Reason = "x" }, null));
    }

    // ------------------------------------------------------------------ Đổi ca

    [SkippableFact]
    public async Task Shift_swap_waits_for_the_colleague_and_checks_conflicts()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var morning = await NewShiftAsync(s, 8, 12);
        var afternoon = await NewShiftAsync(s, 13, 17);
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s);
        var aMorning = await NewScheduleAsync(s, a, morning, Day);
        var bAfternoon = await NewScheduleAsync(s, b, afternoon, Day.AddDays(1));

        var swap = await As(s, a).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "Trùng lịch thi", SourceScheduleId = aMorning.Id, TargetScheduleId = bAfternoon.Id
        }, null);
        Assert.Equal(RequestStatus.PendingPeer, swap.Status);
        Assert.Equal(b.Id, swap.TargetUser!.Id);
        Assert.Equal(aMorning.Id, swap.SourceSchedule!.Id);
        Assert.Equal("08:00", swap.SourceSchedule.StartTime);
        Assert.Equal(bAfternoon.Id, swap.TargetSchedule!.Id);
        Assert.Equal((Day, Day.AddDays(1)), (swap.StartDate, swap.EndDate));

        // Đồng nghiệp thấy đơn chờ mình trả lời; ca đang dính đơn khác thì không đổi tiếp được
        var incoming = await As(s, b).GetPeerRequestsAsync(new LeaveRequestFilter { Status = RequestStatus.PendingPeer });
        var row = Assert.Single(incoming.Items);
        Assert.True(row.CanRespond);
        Assert.False(row.CanCancel);
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, a).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "x", SourceScheduleId = aMorning.Id, TargetUserId = b.Id
        }, null));

        // Chỉ người được đề nghị mới trả lời được
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            As(s, a).RespondAsPeerAsync(swap.Id, new PeerResponseRequest { Accepted = true }));
        var accepted = await As(s, b).RespondAsPeerAsync(swap.Id, new PeerResponseRequest { Accepted = true, Note = "OK bạn" });
        Assert.Equal(RequestStatus.Pending, accepted.Status);
        Assert.Equal("OK bạn", accepted.PeerNote);
        Assert.Equal(Now, accepted.PeerRespondedAt);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            As(s, b).RespondAsPeerAsync(swap.Id, new PeerResponseRequest { Accepted = false }));

        // Nhờ làm thay: người đã có ca chồng giờ thì không nhận được; ca nháp không đổi được
        var c = await NewEmployeeAsync(s);
        var d = await NewEmployeeAsync(s);
        var cMorning = await NewScheduleAsync(s, c, morning, Day.AddDays(2));
        var dLate = await NewScheduleAsync(s, d, await NewShiftAsync(s, 10, 14), Day.AddDays(2));
        var conflict = await Assert.ThrowsAsync<BadRequestException>(() => As(s, c).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "x", SourceScheduleId = cMorning.Id, TargetUserId = d.Id
        }, null));
        Assert.Contains("Đồng nghiệp đã có", conflict.Message);
        var draft = await NewScheduleAsync(s, c, afternoon, Day.AddDays(3), published: false);
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, c).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "x", SourceScheduleId = draft.Id, TargetUserId = b.Id
        }, null));
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, c).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "x", SourceScheduleId = dLate.Id, TargetUserId = b.Id // ca của người khác
        }, null));

        var cover = await As(s, c).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "Đi khám bệnh", SourceScheduleId = cMorning.Id, TargetUserId = b.Id
        }, null);
        Assert.Null(cover.TargetSchedule);
        var declined = await As(s, b).RespondAsPeerAsync(cover.Id, new PeerResponseRequest { Accepted = false, Note = "Em bận" });
        Assert.Equal(RequestStatus.PeerDeclined, declined.Status);
        Assert.False(declined.CanCancel);
    }

    // ------------------------------------------------------------------ Giải trình

    [SkippableFact]
    public async Task Explanations_target_own_late_early_or_pending_logs_once()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s, 8, 17);
        var yesterday = Now.Date.AddDays(-1);
        var logs = new AttendanceLogRepository(s);

        async Task<AttendanceLog> NewLogAsync(DateTime day, int late)
        {
            var schedule = await NewScheduleAsync(s, employee, shift, day);
            var log = new AttendanceLog
            {
                ScheduleId = schedule.Id, UserId = employee.Id, BranchId = Branch1, WorkDate = day,
                CheckinTime = day.AddHours(8).AddMinutes(late), CheckinImageUrl = "https://img.test/in.jpg",
                CheckinLat = 21.0316m, CheckinLng = 105.7831m, CheckinDistance = 10, CheckinFaceMatched = true,
                LateMinutes = late, Status = AttendanceStatus.Valid
            };
            await logs.AddAsync(log);
            return log;
        }

        var lateLog = await NewLogAsync(yesterday, 12);
        var onTime = await NewLogAsync(yesterday.AddDays(-1), 0);
        var explanationType = (await new LeaveTypeRepository(s).GetListAsync(activeOnly: true))
            .First(t => t.Category == RequestCategory.Explanation).Id;
        CreateLeaveRequestForm Explain(long logId) => new()
        {
            LeaveTypeId = explanationType, Reason = "Kẹt xe trên đường Phạm Hùng", AttendanceLogId = logId
        };

        var explanation = await As(s, employee).CreateAsync(Explain(lateLog.Id), null);
        Assert.Equal(RequestCategory.Explanation, explanation.Category);
        Assert.Equal(lateLog.Id, explanation.AttendanceLog!.Id);
        Assert.Equal(12, explanation.AttendanceLog.LateMinutes);
        Assert.Equal("Ca 8h-17h", explanation.AttendanceLog.ShiftName);

        await Assert.ThrowsAsync<BadRequestException>(() => As(s, employee).CreateAsync(Explain(lateLog.Id), null)); // đã giải trình
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, employee).CreateAsync(Explain(onTime.Id), null)); // đúng giờ
        var other = await NewEmployeeAsync(s);
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, other).CreateAsync(Explain(lateLog.Id), null)); // không phải của mình
    }

    // ------------------------------------------------------------------ Phạm vi xem

    [SkippableFact]
    public async Task Managers_see_requests_of_their_branch_only()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewEmployeeAsync(s, Branch1);
        var b = await NewEmployeeAsync(s, Branch2);
        var ra = await As(s, a).CreateAsync(Leave(AnnualLeave, Day.AddDays(20), Day.AddDays(20)), null);
        var rb = await As(s, b).CreateAsync(Leave(AnnualLeave, Day.AddDays(20), Day.AddDays(20)), null);
        var window = new LeaveRequestFilter { FromDate = DateOnly.FromDateTime(Day.AddDays(20)), ToDate = DateOnly.FromDateTime(Day.AddDays(20)), PageSize = 100 };

        var manager = Service(s, new TestUser(ManagerId, Branch1, RoleNames.Manager));
        var managed = await manager.GetManagedAsync(window);
        Assert.Contains(managed.Items, i => i.Id == ra.Id);
        Assert.DoesNotContain(managed.Items, i => i.Id == rb.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetManagedAsync(new LeaveRequestFilter { BranchId = Branch2 }));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetByIdAsync(rb.Id));
        Assert.Equal(ra.Id, (await manager.GetByIdAsync(ra.Id)).Id);

        var hr = Service(s, new TestUser(HrId, null, RoleNames.Hr));
        var all = await hr.GetManagedAsync(window);
        Assert.Contains(all.Items, i => i.Id == ra.Id);
        Assert.Contains(all.Items, i => i.Id == rb.Id);
        var bySearch = await hr.GetManagedAsync(new LeaveRequestFilter { Search = b.EmployeeCode.ToLowerInvariant() });
        Assert.Equal(rb.Id, Assert.Single(bySearch.Items).Id);
        await Assert.ThrowsAsync<BadRequestException>(() => hr.GetManagedAsync(new LeaveRequestFilter { Status = "waiting" }));
    }
}
