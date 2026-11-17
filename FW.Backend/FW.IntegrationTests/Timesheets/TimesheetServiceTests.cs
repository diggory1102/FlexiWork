using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using FW.Application.Features.Attendance;
using FW.Application.Features.Scheduling;
using FW.Application.Features.Timesheets;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.IntegrationTests.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FW.IntegrationTests.Timesheets;

/// <summary>
/// Bảng công tháng (Task 5.3) trên MySQL thật: tổng hợp từ lịch / chấm công / nghỉ / OT, phạm vi theo vai trò, quy trình
/// nháp → xác nhận → khóa → mở lại, chặn duyệt khi đã khóa. Đồng hồ cố định 10/04/2035; dữ liệu nằm ở tháng 3/2035 (đã kết thúc).
/// Mỗi test tạo nhân viên riêng và lọc theo <c>UserIds</c> nên không ảnh hưởng nhau. Người dùng 2 = Quản lý chi nhánh 1, 4 = HR.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TimesheetServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, ManagerId = 2, HrId = 4;
    private const int AnnualLeave = 1, UnpaidLeave = 3, Overtime = 5;
    private static readonly DateTime Now = new(2035, 4, 10, 10, 0, 0);
    private static readonly DateTime March = new(2035, 3, 1);

    private readonly DatabaseFixture _db;

    public TimesheetServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => TimesheetServiceTests.Now;
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

    private static readonly TestUser Manager1 = new(ManagerId, Branch1, RoleNames.Manager);
    private static readonly TestUser Manager2 = new(ManagerId, Branch2, RoleNames.Manager);
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);

    private static TimesheetService Service(DbSession s, ICurrentUserService user) => new(
        new TimesheetRepository(s), new ScheduleRepository(s), new ShiftRepository(s), new AttendanceLogRepository(s), new LeaveRequestRepository(s),
        new UserRepository(s), new PositionRepository(s), new BranchRepository(s),
        new AttendancePolicyService(new ViolationRuleRepository(s), new SystemSettingRepository(s), s), s, user, new FixedClock(),
        Options.Create(new AttendanceSettings()), NullLogger<TimesheetService>.Instance);

    private static AttendanceLogService Logs(DbSession s, ICurrentUserService user) =>
        new(new AttendanceLogRepository(s), new RecordingNotifier(), new TimesheetGuard(new TimesheetRepository(s)), user, new FixedClock());

    // ------------------------------------------------------------------ dữ liệu

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId = Branch1)
    {
        var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static async Task<Shift> NewShiftAsync(DbSession s, int startHour = 8, int endHour = 17)
    {
        var shift = new Shift
        {
            ShiftCode = $"TS{Guid.NewGuid():N}"[..14], Name = $"Ca {startHour}h-{endHour}h", BranchId = Branch1,
            StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour)
        };
        await new ShiftRepository(s).AddAsync(shift);
        return shift;
    }

    private static async Task<Schedule> NewScheduleAsync(DbSession s, User user, Shift shift, DateTime day)
    {
        var schedule = new Schedule
        {
            UserId = user.Id, ShiftId = shift.Id, BranchId = user.BranchId ?? Branch1, WorkDate = day, IsPublished = true, PublishedAt = day.AddDays(-5)
        };
        await new ScheduleRepository(s).AddAsync(schedule);
        return schedule;
    }

    private static async Task<AttendanceLog> NewLogAsync(DbSession s, User user, Schedule schedule, string status = AttendanceStatus.Valid,
        bool checkedOut = true, int lateMinutes = 0)
    {
        var day = schedule.WorkDate.Date;
        var log = new AttendanceLog
        {
            ScheduleId = schedule.Id, UserId = user.Id, BranchId = schedule.BranchId, WorkDate = day,
            CheckinTime = day.AddHours(8).AddMinutes(lateMinutes), CheckinImageUrl = "https://img.test/in.jpg", CheckinLat = 21.0316m, CheckinLng = 105.7831m,
            CheckinDistance = 10, CheckinWifiMatched = true, CheckinFaceMatched = true, LateMinutes = lateMinutes,
            CheckoutTime = checkedOut ? day.AddHours(17) : null, ActualWorkHours = checkedOut ? 8 : 0, Status = status
        };
        await new AttendanceLogRepository(s).AddAsync(log);
        return log;
    }

    private static Task AddLeaveAsync(DbSession s, User user, int typeId, DateTime from, DateTime to) => new LeaveRequestRepository(s).AddAsync(new LeaveRequest
    {
        UserId = user.Id, BranchId = user.BranchId, LeaveTypeId = typeId, StartDate = from, EndDate = to,
        DurationDays = (decimal)(to - from).TotalDays + 1, Reason = "Việc gia đình", Status = RequestStatus.Approved, ApprovedBy = ManagerId, ApprovedAt = Now
    });

    private static Task AddOvertimeAsync(DbSession s, User user, DateTime day, int fromHour, int toHour) => new LeaveRequestRepository(s).AddAsync(new LeaveRequest
    {
        UserId = user.Id, BranchId = user.BranchId, LeaveTypeId = Overtime, StartDate = day, EndDate = day, DurationDays = 0,
        StartAt = day.AddHours(fromHour), EndAt = day.AddHours(toHour), DurationHours = toHour - fromHour, Reason = "Kiểm kho",
        Status = RequestStatus.Approved, ApprovedBy = ManagerId, ApprovedAt = Now
    });

    private static CalculateTimesheetsRequest For(User user, int? branchId = null) =>
        new() { Year = 2035, Month = 3, BranchId = branchId, UserIds = new List<int> { user.Id } };

    private static async Task<TimesheetDto> SheetAsync(TimesheetService service, User user) =>
        Assert.Single((await service.GetPagedAsync(new TimesheetFilter { Year = 2035, Month = 3, Search = user.EmployeeCode })).Items);

    // ------------------------------------------------------------------ Tổng hợp

    [SkippableFact]
    public async Task Calculate_aggregates_shifts_logs_leave_overtime_and_violations()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s);
        var hr = Service(s, Hr);

        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(1)));                                  // đúng giờ
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(2)), lateMinutes: 20);               // muộn 20' → phạt 50.000
        await NewScheduleAsync(s, employee, shift, March.AddDays(3));                                                                  // vắng
        await NewScheduleAsync(s, employee, shift, March.AddDays(4));                                                                  // nghỉ phép năm
        await NewScheduleAsync(s, employee, shift, March.AddDays(5));                                                                  // nghỉ không lương
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(6)), AttendanceStatus.PendingApproval); // chờ duyệt
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(7)), AttendanceStatus.Invalid);         // bị từ chối
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(8)), checkedOut: false);                // thiếu check-out
        await AddLeaveAsync(s, employee, AnnualLeave, March.AddDays(4), March.AddDays(4));
        await AddLeaveAsync(s, employee, UnpaidLeave, March.AddDays(5), March.AddDays(5));
        await AddOvertimeAsync(s, employee, March.AddDays(1), 18, 20);

        var result = await hr.CalculateAsync(For(employee));
        Assert.Equal(1, result.Calculated);

        var sheet = await SheetAsync(hr, employee);
        Assert.Equal((TimesheetStatus.Draft, 2035, 3, Branch1), (sheet.Status, sheet.Year, sheet.Month, sheet.BranchId));
        var f = sheet.Figures;
        Assert.Equal((8, 2, 1, 1, 1, 1), (f.ScheduledShifts, f.WorkedShifts, f.AbsentShifts, f.PendingLogs, f.RejectedLogs, f.MissingCheckouts));
        Assert.Equal((2m, 16m, 20, 1, 50000m), (f.ActualWorkDays, f.ActualWorkHours, f.TotalLateMinutes, f.ViolationCount, f.PenaltyAmount));
        Assert.Equal((2m, 1m, 1m), (f.TotalOvertimeHours, f.PaidLeaveDays, f.UnpaidLeaveDays));

        var detail = await hr.GetDetailAsync(sheet.Id!.Value);
        Assert.Equal(8, detail.Lines.Count);
        Assert.Equal(new[] { "worked", "worked", "absent", "leave", "leave", "pending", "rejected", "missing_checkout" }, detail.Lines.Select(l => l.Outcome));
        Assert.Equal(new[] { "paid", "unpaid" }, detail.Lines.Where(l => l.Outcome == "leave").Select(l => l.LeaveKind));
        Assert.Single(detail.Overtime);
        Assert.False(detail.CanConfirm); // còn lượt chấm công chờ duyệt
        Assert.False(detail.Drifted);

        // Tính lại là idempotent: vẫn một bảng công
        await hr.CalculateAsync(For(employee));
        Assert.Equal(1, (await hr.GetPagedAsync(new TimesheetFilter { Year = 2035, Month = 3, Search = employee.EmployeeCode })).TotalCount);
    }

    [SkippableFact]
    public async Task Approved_explanation_exempts_the_late_violation()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var log = await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, await NewShiftAsync(s), March.AddDays(2)), lateMinutes: 20);
        var hr = Service(s, Hr);

        await hr.CalculateAsync(For(employee));
        Assert.Equal(50000m, (await SheetAsync(hr, employee)).Figures.PenaltyAmount);

        var explanationType = (await new LeaveTypeRepository(s).GetListAsync(activeOnly: true)).First(t => t.Category == RequestCategory.Explanation).Id;
        await new LeaveRequestRepository(s).AddAsync(new LeaveRequest
        {
            UserId = employee.Id, BranchId = Branch1, LeaveTypeId = explanationType, StartDate = March.AddDays(2), EndDate = March.AddDays(2),
            AttendanceLogId = log.Id, Reason = "Kẹt xe", Status = RequestStatus.Approved, ApprovedBy = ManagerId, ApprovedAt = Now
        });

        await hr.CalculateAsync(For(employee));
        var figures = (await SheetAsync(hr, employee)).Figures;
        Assert.Equal((0m, 0, 0), (figures.PenaltyAmount, figures.TotalLateMinutes, figures.ViolationCount));
        Assert.Equal(1m, figures.ActualWorkDays);
    }

    [SkippableFact]
    public async Task Calculation_rejects_future_months_and_leaves_employees_without_shifts_out()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var idle = await NewEmployeeAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, await NewShiftAsync(s), March.AddDays(1)));
        var hr = Service(s, Hr);

        await Assert.ThrowsAsync<BadRequestException>(() => hr.CalculateAsync(new CalculateTimesheetsRequest { Year = 2035, Month = 5 }));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.CalculateAsync(new CalculateTimesheetsRequest { Year = 2035, Month = 13 }));

        var request = new CalculateTimesheetsRequest { Year = 2035, Month = 3, UserIds = new List<int> { employee.Id, idle.Id } };
        Assert.Equal(1, (await hr.CalculateAsync(request)).Calculated);
        Assert.Empty((await hr.GetPagedAsync(new TimesheetFilter { Year = 2035, Month = 3, Search = idle.EmployeeCode })).Items);
    }

    // ------------------------------------------------------------------ Phạm vi

    [SkippableFact]
    public async Task Scope_follows_roles_and_branches()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var other = await NewEmployeeAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, await NewShiftAsync(s), March.AddDays(1)));
        var hr = Service(s, Hr);
        await hr.CalculateAsync(For(employee));
        var sheet = await SheetAsync(hr, employee);

        // Quản lý chi nhánh 1 thấy; Quản lý chi nhánh 2 không thấy và không tính / xem được bảng công chi nhánh 1
        var manager1 = Service(s, Manager1);
        Assert.Single((await manager1.GetPagedAsync(new TimesheetFilter { Year = 2035, Month = 3, Search = employee.EmployeeCode })).Items);
        var manager2 = Service(s, Manager2);
        Assert.Empty((await manager2.GetPagedAsync(new TimesheetFilter { Year = 2035, Month = 3, Search = employee.EmployeeCode })).Items);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager2.GetDetailAsync(sheet.Id!.Value));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager2.CalculateAsync(For(employee, Branch1)));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager1.GetSummaryAsync(2035, 3, Branch2));

        // Nhân viên: chỉ xem bảng công của mình, không xem danh sách
        var self = Service(s, new TestUser(employee.Id, Branch1));
        await Assert.ThrowsAsync<ForbiddenException>(() => self.GetPagedAsync(new TimesheetFilter { Year = 2035, Month = 3 }));
        Assert.Equal(sheet.Id, (await self.GetDetailAsync(sheet.Id!.Value)).Timesheet.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, new TestUser(other.Id, Branch1)).GetDetailAsync(sheet.Id!.Value));
    }

    [SkippableFact]
    public async Task My_timesheet_is_a_live_preview_until_confirmed()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(1)));
        var self = Service(s, new TestUser(employee.Id, Branch1));

        var preview = await self.GetMineAsync(new MyTimesheetFilter { Year = 2035, Month = 3 });
        Assert.True(preview.IsPreview);
        Assert.Null(preview.Id);
        Assert.Equal((1, 1m), (preview.Figures.WorkedShifts, preview.Figures.ActualWorkDays));

        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(2)));
        Assert.Equal(2, (await self.GetMineAsync(new MyTimesheetFilter { Year = 2035, Month = 3 })).Figures.WorkedShifts); // luôn mới nhất

        await Service(s, Manager1).ConfirmAsync(For(employee));
        var stored = await self.GetMineAsync(new MyTimesheetFilter { Year = 2035, Month = 3 });
        Assert.False(stored.IsPreview);
        Assert.Equal(TimesheetStatus.Confirmed, stored.Status);
        Assert.NotNull(stored.ConfirmedByName);

        // Tháng chưa có dữ liệu: xem trước rỗng
        var empty = await self.GetMineAsync(new MyTimesheetFilter { Year = 2035, Month = 4 });
        Assert.True(empty.IsPreview);
        Assert.Equal(0, empty.Figures.ScheduledShifts);
    }

    // ------------------------------------------------------------------ Quy trình

    [SkippableFact]
    public async Task Confirm_is_blocked_by_pending_logs_and_by_own_timesheet()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(1)));
        var pending = await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(2)), AttendanceStatus.PendingApproval);
        var manager = Service(s, Manager1);

        var blocked = await manager.ConfirmAsync(For(employee));
        Assert.Equal(0, blocked.Confirmed);
        var blocker = Assert.Single(blocked.Blocked);
        Assert.Equal(employee.Id, blocker.UserId);
        Assert.Contains("chờ duyệt", blocker.Reason);

        // Duyệt lượt chờ rồi xác nhận được; số liệu được tính lại ngay lúc xác nhận
        await Logs(s, Manager1).ReviewAsync(pending.Id, new ReviewAttendanceRequest { Approved = true });
        var confirmed = await manager.ConfirmAsync(For(employee));
        Assert.Equal(1, confirmed.Confirmed);
        var sheet = await SheetAsync(manager, employee);
        Assert.Equal((TimesheetStatus.Confirmed, 2), (sheet.Status, sheet.Figures.WorkedShifts));
        Assert.NotNull(sheet.ConfirmedAt);
        Assert.NotNull(sheet.ConfirmedByName);

        // Đã xác nhận: tính lại bỏ qua, xác nhận lại không đổi
        var again = await manager.CalculateAsync(For(employee));
        Assert.Equal((0, 1), (again.Calculated, again.SkippedConfirmed));
        Assert.Equal(0, (await manager.ConfirmAsync(For(employee))).Confirmed);

        // Không ai tự xác nhận bảng công của chính mình
        var managerSelf = new TestUser(ManagerId, Branch1, RoleNames.Manager);
        var own = await new UserRepository(s).GetByIdAsync(ManagerId);
        await NewLogAsync(s, own!, await NewScheduleAsync(s, own!, shift, March.AddDays(3)));
        var selfResult = await Service(s, managerSelf).ConfirmAsync(new CalculateTimesheetsRequest { Year = 2035, Month = 3, UserIds = new List<int> { ManagerId } });
        Assert.Equal(0, selfResult.Confirmed);
        Assert.Contains("tự xác nhận", Assert.Single(selfResult.Blocked).Reason);
    }

    [SkippableFact]
    public async Task Lock_requires_hr_confirmed_sheets_and_a_finished_month()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var draft = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(1)));
        await NewLogAsync(s, draft, await NewScheduleAsync(s, draft, shift, March.AddDays(1)));
        var manager = Service(s, Manager1);
        var hr = Service(s, Hr);
        var both = new CalculateTimesheetsRequest { Year = 2035, Month = 3, UserIds = new List<int> { employee.Id, draft.Id } };

        await hr.CalculateAsync(both);
        await manager.ConfirmAsync(For(employee)); // chỉ xác nhận một người

        await Assert.ThrowsAsync<ForbiddenException>(() => manager.LockAsync(For(employee)));
        var tooEarly = await Assert.ThrowsAsync<BadRequestException>(() => hr.LockAsync(new CalculateTimesheetsRequest { Year = 2035, Month = 4 }));
        Assert.Contains("chưa kết thúc", tooEarly.Message);

        var locked = await hr.LockAsync(both);
        Assert.Equal(1, locked.Locked);
        Assert.True(locked.RemainingDrafts >= 1);
        Assert.Equal(TimesheetStatus.Locked, (await SheetAsync(hr, employee)).Status);
        Assert.Equal(TimesheetStatus.Draft, (await SheetAsync(hr, draft)).Status);
        Assert.NotNull((await SheetAsync(hr, employee)).LockedAt);

        // Đã khóa: tính lại bỏ qua
        var recalculated = await hr.CalculateAsync(For(employee));
        Assert.Equal((0, 1), (recalculated.Calculated, recalculated.SkippedFrozen));
    }

    [SkippableFact]
    public async Task Reopen_follows_roles_and_requires_a_reason()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, await NewShiftAsync(s), March.AddDays(1)));
        var manager = Service(s, Manager1);
        var hr = Service(s, Hr);

        await manager.ConfirmAsync(For(employee));
        var sheet = await SheetAsync(hr, employee);
        var id = sheet.Id!.Value;

        await Assert.ThrowsAsync<BadRequestException>(() => manager.ReopenAsync(id, new ReopenTimesheetRequest { Note = "  " }));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, Manager2).ReopenAsync(id, new ReopenTimesheetRequest { Note = "Sửa" }));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, new TestUser(employee.Id, Branch1)).ReopenAsync(id, new ReopenTimesheetRequest { Note = "Sửa" }));

        var reopened = await manager.ReopenAsync(id, new ReopenTimesheetRequest { Note = "Thiếu một ca OT" });
        Assert.Equal((TimesheetStatus.Draft, "Thiếu một ca OT"), (reopened.Status, reopened.Note));
        Assert.Null(reopened.ConfirmedAt);
        await Assert.ThrowsAsync<BadRequestException>(() => manager.ReopenAsync(id, new ReopenTimesheetRequest { Note = "Lần nữa" })); // đang là nháp

        // Đã khóa: Quản lý không mở được, HR mở được
        await manager.ConfirmAsync(For(employee));
        await hr.LockAsync(For(employee));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.ReopenAsync(id, new ReopenTimesheetRequest { Note = "Sửa" }));
        Assert.Equal(TimesheetStatus.Draft, (await hr.ReopenAsync(id, new ReopenTimesheetRequest { Note = "HR mở lại" })).Status);
        Assert.Null((await SheetAsync(hr, employee)).LockedAt);
    }

    [SkippableFact]
    public async Task Detail_flags_drift_when_source_data_changes_after_confirmation()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(1)));
        var manager = Service(s, Manager1);
        await manager.ConfirmAsync(For(employee));
        var id = (await SheetAsync(manager, employee)).Id!.Value;

        var before = await manager.GetDetailAsync(id);
        Assert.False(before.Drifted);
        Assert.True(before.CanReopen);
        Assert.False(before.CanConfirm);

        await AddOvertimeAsync(s, employee, March.AddDays(1), 18, 21); // OT duyệt thêm sau khi xác nhận
        var after = await manager.GetDetailAsync(id);
        Assert.True(after.Drifted);
        Assert.Equal(3m, after.Live.TotalOvertimeHours);
        Assert.Equal(0m, after.Timesheet.Figures.TotalOvertimeHours); // số liệu đã lưu không đổi
    }

    // ------------------------------------------------------------------ Guard

    [SkippableFact]
    public async Task Locked_months_block_further_approvals_but_not_rejections()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var shift = await NewShiftAsync(s);
        await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(1)));
        var pending = await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(2)), AttendanceStatus.PendingApproval);
        var rejectedLater = await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(3)), AttendanceStatus.PendingApproval);

        // Xác nhận khi còn lượt chờ bị chặn → duyệt 2 lượt trước, khóa, rồi tạo thêm lượt chờ mới sau khi khóa
        await Logs(s, Manager1).ReviewAsync(pending.Id, new ReviewAttendanceRequest { Approved = true });
        await Logs(s, Manager1).ReviewAsync(rejectedLater.Id, new ReviewAttendanceRequest { Approved = true });
        await Service(s, Manager1).ConfirmAsync(For(employee));
        await Service(s, Hr).LockAsync(For(employee));

        var late = await NewLogAsync(s, employee, await NewScheduleAsync(s, employee, shift, March.AddDays(4)), AttendanceStatus.PendingApproval);
        var blocked = await Assert.ThrowsAsync<BadRequestException>(() =>
            Logs(s, Manager1).ReviewAsync(late.Id, new ReviewAttendanceRequest { Approved = true }));
        Assert.Contains("đã khóa", blocked.Message);
        Assert.Equal(AttendanceStatus.PendingApproval, (await new AttendanceLogRepository(s).GetByIdAsync(late.Id))!.Status);

        // Mở lại rồi duyệt được
        var id = (await SheetAsync(Service(s, Hr), employee)).Id!.Value;
        await Service(s, Hr).ReopenAsync(id, new ReopenTimesheetRequest { Note = "Bổ sung lượt chấm công" });
        var approved = await Logs(s, Manager1).ReviewAsync(late.Id, new ReviewAttendanceRequest { Approved = true });
        Assert.Equal(AttendanceStatus.Valid, approved.Log.Status);
    }
}
