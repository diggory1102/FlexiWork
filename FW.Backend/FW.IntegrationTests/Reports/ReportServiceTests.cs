using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Reports;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.Reports;

/// <summary>
/// Dashboard và báo cáo (Task 5.6) trên MySQL thật. Đồng hồ cố định 10/03/2037 10:30 (ca 08–12 đang diễn ra, ca 06–09 đã qua, ca 13–17 chưa tới);
/// dữ liệu nằm ở năm 2037 để không lẫn với test khác. Số "chờ duyệt" tính trên toàn hệ thống nên so theo chênh lệch.
/// Người dùng 2 = Quản lý chi nhánh 1, 4 = HR.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ReportServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, ManagerId = 2, HrId = 4;
    private static readonly DateTime Now = new(2037, 3, 10, 10, 30, 0);
    private static readonly DateTime Today = new(2037, 3, 10);

    private readonly DatabaseFixture _db;

    public ReportServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => ReportServiceTests.Now;
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
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);
    private static readonly TestUser Staff = new(3, Branch1);

    private static DashboardService Dashboard(DbSession s, ICurrentUserService user) =>
        new(new ReportRepository(s), new BranchRepository(s), user, new FixedClock());

    private static ViolationReportService Violations(DbSession s, ICurrentUserService user) => new(new ReportRepository(s),
        new AttendancePolicyService(new ViolationRuleRepository(s), new SystemSettingRepository(s), s), new UserRepository(s), user, new FixedClock());

    private static LeaveUsageReportService LeaveUsage(DbSession s, ICurrentUserService user) =>
        new(new ReportRepository(s), new SystemSettingRepository(s), user, new FixedClock());

    // ------------------------------------------------------------------ dữ liệu

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId = Branch1)
    {
        var code = $"B{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static async Task<Shift> NewShiftAsync(DbSession s, int startHour, int endHour)
    {
        var shift = new Shift
        {
            ShiftCode = $"RP{Guid.NewGuid():N}"[..14], Name = $"Ca {startHour}h-{endHour}h", BranchId = Branch1,
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

    private static async Task<AttendanceLog> NewLogAsync(DbSession s, User user, Schedule schedule, int startHour, string status = AttendanceStatus.Valid,
        int lateMinutes = 0, DateTime? checkout = null, int earlyMinutes = 0)
    {
        var day = schedule.WorkDate.Date;
        var log = new AttendanceLog
        {
            ScheduleId = schedule.Id, UserId = user.Id, BranchId = schedule.BranchId, WorkDate = day,
            CheckinTime = day.AddHours(startHour).AddMinutes(lateMinutes), CheckinImageUrl = "https://img.test/in.jpg", CheckinLat = 21.0316m, CheckinLng = 105.7831m,
            CheckinDistance = 10, CheckinWifiMatched = true, CheckinFaceMatched = true, LateMinutes = lateMinutes, EarlyMinutes = earlyMinutes,
            CheckoutTime = checkout, ActualWorkHours = checkout.HasValue ? 8 : 0, Status = status
        };
        await new AttendanceLogRepository(s).AddAsync(log);
        return log;
    }

    private static Task AddLeaveAsync(DbSession s, User user, int typeId, DateTime from, DateTime to, string status = RequestStatus.Approved) =>
        new LeaveRequestRepository(s).AddAsync(new LeaveRequest
        {
            UserId = user.Id, BranchId = user.BranchId, LeaveTypeId = typeId, StartDate = from, EndDate = to, DurationDays = (decimal)(to - from).TotalDays + 1,
            Reason = "Việc gia đình", Status = status
        });

    // ------------------------------------------------------------------ Dashboard

    [SkippableFact]
    public async Task Dashboard_counts_headcount_by_branch_and_state()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var hr = Dashboard(s, Hr);
        var baseline = await hr.GetAsync(new DashboardFilter { Date = DateOnly.FromDateTime(Today) });

        var morning = await NewShiftAsync(s, 8, 12);
        var early = await NewShiftAsync(s, 6, 9);
        var afternoon = await NewShiftAsync(s, 13, 17);
        var (a, b, c, d, e, f, g, h, i, j) = (await NewEmployeeAsync(s), await NewEmployeeAsync(s), await NewEmployeeAsync(s), await NewEmployeeAsync(s),
            await NewEmployeeAsync(s), await NewEmployeeAsync(s), await NewEmployeeAsync(s), await NewEmployeeAsync(s), await NewEmployeeAsync(s, Branch2),
            await NewEmployeeAsync(s));

        await NewLogAsync(s, a, await NewScheduleAsync(s, a, morning, Today), 8);                                                   // đang làm
        await NewLogAsync(s, b, await NewScheduleAsync(s, b, morning, Today), 8, lateMinutes: 20);                                  // đang làm, đi muộn
        await NewScheduleAsync(s, c, morning, Today);                                                                               // chưa chấm công
        await NewScheduleAsync(s, d, early, Today);                                                                                 // vắng mặt
        await NewScheduleAsync(s, e, morning, Today, ScheduleStatus.OnLeave);                                                       // nghỉ phép
        await NewScheduleAsync(s, f, afternoon, Today);                                                                             // chưa tới ca
        await NewLogAsync(s, g, await NewScheduleAsync(s, g, early, Today), 6, checkout: Today.AddHours(9));                        // đã xong ca
        await NewLogAsync(s, h, await NewScheduleAsync(s, h, morning, Today), 8, AttendanceStatus.PendingApproval);                 // chờ duyệt
        await NewLogAsync(s, i, await NewScheduleAsync(s, i, morning, Today), 8);                                                   // chi nhánh 2, đang làm
        await NewScheduleAsync(s, j, early, Today.AddDays(-1));                                                                     // hôm qua vắng mặt
        await AddLeaveAsync(s, f, 1, Today.AddDays(30), Today.AddDays(30), RequestStatus.Pending);                                  // đơn chờ duyệt

        var result = await hr.GetAsync(new DashboardFilter { Date = DateOnly.FromDateTime(Today) });
        Assert.Equal(Now, result.AsOf);
        var branch1 = result.Branches.Single(x => x.BranchId == Branch1).Headcount;
        Assert.Equal((8, 8, 4, 3), (branch1.ScheduledEmployees, branch1.ScheduledShifts, branch1.CheckedInEmployees, branch1.WorkingEmployees));
        Assert.Equal((1, 1, 1, 1, 1), (branch1.WaitingCheckInShifts, branch1.UpcomingShifts, branch1.AbsentShifts, branch1.OnLeaveEmployees, branch1.CompletedShifts));
        Assert.Equal((1, 75.0m), (branch1.LateShifts, branch1.OnTimeRate)); // 3 / 4 lượt có mặt đúng giờ
        Assert.Equal(baseline.Branches.Single(x => x.BranchId == Branch1).Headcount.PendingAttendance + 1, branch1.PendingAttendance);
        Assert.Equal(baseline.Branches.Single(x => x.BranchId == Branch1).Headcount.PendingRequests + 1, branch1.PendingRequests);

        var branch2 = result.Branches.Single(x => x.BranchId == Branch2).Headcount;
        Assert.Equal((1, 1, 1, 100.0m), (branch2.ScheduledShifts, branch2.WorkingEmployees, branch2.CheckedInEmployees, branch2.OnTimeRate));

        Assert.Equal((9, 9, 5, 4), (result.Totals.ScheduledShifts, result.Totals.ScheduledEmployees, result.Totals.CheckedInEmployees, result.Totals.WorkingEmployees));
        Assert.Equal((1, 80.0m), (result.Totals.LateShifts, result.Totals.OnTimeRate)); // 4 / 5

        // Xu hướng 7 ngày tới ngày xem: hôm qua có một ca vắng mặt
        Assert.Equal(7, result.Trend.Count);
        Assert.Equal(Today.AddDays(-6), result.Trend[0].Date);
        var yesterday = result.Trend[^2];
        Assert.Equal((Today.AddDays(-1), 1, 0, 1), (yesterday.Date, yesterday.ScheduledShifts, yesterday.AttendedShifts, yesterday.AbsentShifts));
        Assert.Null(yesterday.OnTimeRate); // chưa ai có mặt
        var todayPoint = result.Trend[^1];
        Assert.Equal((9, 5, 1, 1, 80.0m), (todayPoint.ScheduledShifts, todayPoint.AttendedShifts, todayPoint.LateShifts, todayPoint.AbsentShifts, todayPoint.OnTimeRate));

        // Xem ngày hôm qua: chỉ có ca vắng mặt
        var past = await hr.GetAsync(new DashboardFilter { Date = DateOnly.FromDateTime(Today.AddDays(-1)) });
        Assert.Equal((1, 1), (past.Totals.ScheduledShifts, past.Totals.AbsentShifts));
    }

    [SkippableFact]
    public async Task Dashboard_scope_follows_roles()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, 8, 12);
        var inBranch1 = await NewEmployeeAsync(s);
        var inBranch2 = await NewEmployeeAsync(s, Branch2);
        await NewScheduleAsync(s, inBranch1, shift, Today.AddDays(-2));
        await NewScheduleAsync(s, inBranch2, shift, Today.AddDays(-2));
        var yesterday = new DashboardFilter { Date = DateOnly.FromDateTime(Today.AddDays(-2)) };

        // Quản lý chi nhánh 1: chỉ thấy chi nhánh mình
        var manager = await Dashboard(s, Manager1).GetAsync(yesterday);
        Assert.Equal(Branch1, Assert.Single(manager.Branches).BranchId);
        Assert.True(manager.Totals.ScheduledShifts >= 1);
        Assert.Equal(manager.Branches[0].Headcount.ScheduledShifts, manager.Totals.ScheduledShifts);
        await Assert.ThrowsAsync<ForbiddenException>(() => Dashboard(s, Manager1).GetAsync(new DashboardFilter { BranchId = Branch2, Date = yesterday.Date }));

        // HR: tất cả, hoặc lọc một chi nhánh
        var all = await Dashboard(s, Hr).GetAsync(yesterday);
        Assert.True(all.Branches.Count >= 2);
        var only2 = await Dashboard(s, Hr).GetAsync(new DashboardFilter { BranchId = Branch2, Date = yesterday.Date });
        Assert.Equal(Branch2, Assert.Single(only2.Branches).BranchId);

        // Nhân viên thường: không có quyền
        await Assert.ThrowsAsync<ForbiddenException>(() => Dashboard(s, Staff).GetAsync(yesterday));
    }

    // ------------------------------------------------------------------ Báo cáo vi phạm

    [SkippableFact]
    public async Task Violation_report_counts_late_early_and_absent_shifts_with_exemptions()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, 8, 17);
        var clean = await NewEmployeeAsync(s);
        var v1 = await NewEmployeeAsync(s);
        var v2 = await NewEmployeeAsync(s, Branch2);
        var jan = new DateTime(2037, 1, 1);
        DateTime Day(int n) => jan.AddDays(n);

        await NewLogAsync(s, v1, await NewScheduleAsync(s, v1, shift, Day(1)), 8, lateMinutes: 20, checkout: Day(1).AddHours(17));                         // đi muộn 20'
        await NewLogAsync(s, v1, await NewScheduleAsync(s, v1, shift, Day(2)), 8, checkout: Day(2).AddHours(16).AddMinutes(30), earlyMinutes: 30);          // về sớm 30'
        await NewScheduleAsync(s, v1, shift, Day(3));                                                                                                        // vắng mặt
        await NewScheduleAsync(s, v1, shift, Day(4), ScheduleStatus.OnLeave);                                                                                // nghỉ phép: không vi phạm
        var exempted = await NewLogAsync(s, v1, await NewScheduleAsync(s, v1, shift, Day(5)), 8, lateMinutes: 20, checkout: Day(5).AddHours(17));           // muộn nhưng có giải trình
        await NewLogAsync(s, v1, await NewScheduleAsync(s, v1, shift, Day(6)), 8, checkout: Day(6).AddHours(17));                                           // đúng giờ
        var explanationType = (await new LeaveTypeRepository(s).GetListAsync(activeOnly: true)).First(t => t.Category == RequestCategory.Explanation).Id;
        await new LeaveRequestRepository(s).AddAsync(new LeaveRequest
        {
            UserId = v1.Id, BranchId = Branch1, LeaveTypeId = explanationType, StartDate = Day(5), EndDate = Day(5), AttendanceLogId = exempted.Id,
            Reason = "Kẹt xe", Status = RequestStatus.Approved
        });
        await NewScheduleAsync(s, v2, shift, Day(1));                                                                                                        // v2 vắng mặt
        await NewLogAsync(s, clean, await NewScheduleAsync(s, clean, shift, Day(1)), 8, checkout: Day(1).AddHours(17));

        var rules = await new ViolationRuleRepository(s).GetAllAsync();
        var lateRule = ViolationPolicy.Evaluate(rules, ViolationType.Late, 20, 5);
        var earlyRule = ViolationPolicy.Evaluate(rules, ViolationType.EarlyLeave, 30, 5);
        var report = await Violations(s, Hr).GetReportAsync(new ViolationReportFilter { FromDate = DateOnly.FromDateTime(jan), ToDate = new DateOnly(2037, 1, 31), PageSize = 50 });

        var summary = report.Summary;
        Assert.Equal((8, 7, 5, 1), (summary.ScheduledShifts, summary.EvaluatedShifts, summary.AttendedShifts, summary.OnLeaveShifts)); // 5 có mặt + 2 vắng = 7 ca có kết quả, 1 nghỉ phép
        Assert.Equal((4, 57.1m), (summary.ViolatingShifts, summary.ViolationRate)); // muộn, về sớm, 2 vắng; ca có giải trình không tính
        Assert.Equal((1, 20, 1, 30, 2), (summary.LateCount, summary.LateMinutes, summary.EarlyCount, summary.EarlyMinutes, summary.AbsentCount));
        Assert.Equal(lateRule.PenaltyAmount + earlyRule.PenaltyAmount, summary.PenaltyAmount);
        Assert.Equal(lateRule.DeductWorkUnits + earlyRule.DeductWorkUnits, summary.DeductedWorkDays);
    }

    [SkippableFact]
    public async Task Violation_report_breaks_down_by_branch_and_employee_with_drill_down_and_scope()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, 8, 17);
        var v1 = await NewEmployeeAsync(s);
        var v2 = await NewEmployeeAsync(s, Branch2);
        var clean = await NewEmployeeAsync(s);
        var feb = new DateTime(2037, 2, 1);
        DateTime Day(int n) => feb.AddDays(n);

        await NewLogAsync(s, v1, await NewScheduleAsync(s, v1, shift, Day(1)), 8, lateMinutes: 20, checkout: Day(1).AddHours(17));
        await NewScheduleAsync(s, v1, shift, Day(2));                                              // v1: muộn + vắng = 2 ca vi phạm
        await NewScheduleAsync(s, v2, shift, Day(1));                                              // v2: vắng
        await NewLogAsync(s, clean, await NewScheduleAsync(s, clean, shift, Day(1)), 8, checkout: Day(1).AddHours(17));

        var filter = new ViolationReportFilter { FromDate = DateOnly.FromDateTime(feb), ToDate = new DateOnly(2037, 2, 28), PageSize = 50 };
        var hr = Violations(s, Hr);
        var report = await hr.GetReportAsync(filter);

        Assert.Equal(new[] { Branch1, Branch2 }, report.Branches.Select(b => b.BranchId));
        Assert.Equal((3, 2), (report.Branches[0].Summary.ScheduledShifts, report.Branches[0].Summary.ViolatingShifts)); // v1 (2) + clean (0 vi phạm)
        Assert.Equal((1, 1, 100.0m), (report.Branches[1].Summary.ScheduledShifts, report.Branches[1].Summary.ViolatingShifts, report.Branches[1].Summary.ViolationRate));

        // Theo nhân viên: chỉ người có vi phạm, nhiều nhất trước
        Assert.Equal(new[] { v1.Id, v2.Id }, report.Employees.Items.Select(e => e.UserId));
        Assert.Equal(2, report.Employees.TotalCount);
        Assert.Equal((2, 100.0m), (report.Employees.Items[0].Summary.ViolatingShifts, report.Employees.Items[0].Summary.ViolationRate));
        Assert.Single((await hr.GetReportAsync(new ViolationReportFilter { FromDate = filter.FromDate, ToDate = filter.ToDate, Search = v2.EmployeeCode })).Employees.Items);
        Assert.Equal(1, (await hr.GetReportAsync(new ViolationReportFilter { FromDate = filter.FromDate, ToDate = filter.ToDate, PageSize = 1 })).Employees.Items.Count);

        // Chi tiết từng lượt của một nhân viên: mới nhất trước
        var detail = await hr.GetEmployeeAsync(v1.Id, new EmployeeViolationFilter { FromDate = filter.FromDate, ToDate = filter.ToDate });
        Assert.Equal(new[] { "absent", ViolationType.Late }, detail.Items.Select(i => i.Type));
        Assert.Equal((Day(2), Day(1)), (detail.Items[0].WorkDate, detail.Items[1].WorkDate));
        Assert.Equal(20, detail.Items[1].Minutes);
        Assert.Equal("08:00", detail.Items[1].StartTime);
        Assert.Equal("17:00", detail.Items[1].EndTime);

        // Phạm vi: Quản lý chi nhánh 1 chỉ thấy chi nhánh mình và nhân viên của mình
        var manager = Violations(s, Manager1);
        Assert.Equal(Branch1, Assert.Single((await manager.GetReportAsync(filter)).Branches).BranchId);
        Assert.DoesNotContain((await manager.GetReportAsync(filter)).Employees.Items, e => e.UserId == v2.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetReportAsync(new ViolationReportFilter { BranchId = Branch2, FromDate = filter.FromDate }));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetEmployeeAsync(v2.Id, new EmployeeViolationFilter()));
        await Assert.ThrowsAsync<ForbiddenException>(() => Violations(s, Staff).GetReportAsync(filter));
        await Assert.ThrowsAsync<NotFoundException>(() => hr.GetEmployeeAsync(99_999_999, new EmployeeViolationFilter()));

        // Khoảng ngày
        await Assert.ThrowsAsync<BadRequestException>(() => hr.GetReportAsync(new ViolationReportFilter { FromDate = new DateOnly(2037, 2, 10), ToDate = new DateOnly(2037, 2, 1) }));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.GetReportAsync(new ViolationReportFilter { FromDate = new DateOnly(2037, 1, 1), ToDate = new DateOnly(2037, 6, 1) })); // quá 93 ngày
        Assert.NotNull(await hr.GetReportAsync(new ViolationReportFilter { FromDate = new DateOnly(2037, 1, 1), ToDate = new DateOnly(2037, 3, 10) })); // 69 ngày: hợp lệ
    }

    // ------------------------------------------------------------------ Quỹ phép

    [SkippableFact]
    public async Task Leave_usage_report_summarizes_balances_types_and_months()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var hr = LeaveUsage(s, Hr);
        var baseline = await hr.GetAsync(new LeaveUsageFilter { Year = 2037 });
        var balances = new LeaveBalanceRepository(s);

        var l1 = await NewEmployeeAsync(s);                   // dùng 10/12 ngày → còn 2 (ít phép)
        var l2 = await NewEmployeeAsync(s);                   // chưa có quỹ riêng → mặc định 12
        var l3 = await NewEmployeeAsync(s, Branch2);          // dùng 3/12 ngày
        await balances.EnsureExistsAsync(l1.Id, 2037, 12);
        Assert.True(await balances.TryDeductAsync(l1.Id, 2037, 10));
        await balances.EnsureExistsAsync(l3.Id, 2037, 12);
        Assert.True(await balances.TryDeductAsync(l3.Id, 2037, 3));

        await AddLeaveAsync(s, l1, 1, new DateTime(2037, 3, 2), new DateTime(2037, 3, 4));       // phép năm 3 ngày (tháng 3)
        await AddLeaveAsync(s, l1, 2, new DateTime(2037, 4, 10), new DateTime(2037, 4, 10));     // nghỉ ốm 1 ngày (tháng 4)
        await AddLeaveAsync(s, l3, 3, new DateTime(2037, 3, 20), new DateTime(2037, 3, 21));     // không lương 2 ngày (tháng 3)
        await AddLeaveAsync(s, l2, 1, new DateTime(2037, 5, 4), new DateTime(2037, 5, 5), RequestStatus.Pending); // phép năm chờ duyệt 2 ngày

        var report = await hr.GetAsync(new LeaveUsageFilter { Year = 2037 });
        Assert.Equal((2037, 12m), (report.Year, report.DefaultDays));
        Assert.Equal(baseline.Summary.Employees + 3, report.Summary.Employees);
        Assert.Equal(baseline.Summary.UsedDays + 13, report.Summary.UsedDays);
        Assert.Equal(baseline.Summary.RemainingDays + 2 + 12 + 9, report.Summary.RemainingDays);
        Assert.Equal(baseline.Summary.WithoutBalance + 1, report.Summary.WithoutBalance);
        Assert.Equal(baseline.Summary.LowBalanceEmployees + 1, report.Summary.LowBalanceEmployees);
        Assert.Equal(2m, report.Summary.PendingDays);

        // Theo loại nghỉ (dữ liệu 2037 chỉ có ở test này)
        var annual = report.ByType.Single(t => t.LeaveTypeId == 1);
        Assert.Equal((1, 3m, 2m, true, true), (annual.ApprovedRequests, annual.ApprovedDays, annual.PendingDays, annual.IsPaid, annual.DeductsBalance));
        Assert.Equal((1, 1m), (report.ByType.Single(t => t.LeaveTypeId == 2).ApprovedRequests, report.ByType.Single(t => t.LeaveTypeId == 2).ApprovedDays));
        var unpaid = report.ByType.Single(t => t.LeaveTypeId == 3);
        Assert.Equal((1, 2m, false), (unpaid.ApprovedRequests, unpaid.ApprovedDays, unpaid.IsPaid));

        // Theo tháng: đủ 12 tháng
        Assert.Equal(12, report.ByMonth.Count);
        Assert.Equal((5m, 1m, 0m), (report.ByMonth[2].Days, report.ByMonth[3].Days, report.ByMonth[11].Days));

        // Danh sách nổi bật
        Assert.Equal(new[] { l1.Id, l3.Id }, report.TopUsed.Take(2).Select(e => e.UserId));
        Assert.Equal((10m, 2m), (report.TopUsed[0].UsedDays, report.TopUsed[0].RemainingDays));
        Assert.Equal(l1.Id, Assert.Single(report.LowRemaining).UserId);
        Assert.Contains(report.Branches, b => b.BranchId == Branch1 && b.Employees >= 2);
        Assert.Contains(report.Branches, b => b.BranchId == Branch2);
    }

    [SkippableFact]
    public async Task Leave_usage_report_is_scoped_and_validated()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();

        var manager = await LeaveUsage(s, Manager1).GetAsync(new LeaveUsageFilter { Year = 2037 });
        Assert.All(manager.Branches, b => Assert.Equal(Branch1, b.BranchId));
        Assert.True(manager.Summary.Employees >= 1);

        await Assert.ThrowsAsync<ForbiddenException>(() => LeaveUsage(s, Manager1).GetAsync(new LeaveUsageFilter { BranchId = Branch2 }));
        await Assert.ThrowsAsync<ForbiddenException>(() => LeaveUsage(s, Staff).GetAsync(new LeaveUsageFilter()));
        await Assert.ThrowsAsync<BadRequestException>(() => LeaveUsage(s, Hr).GetAsync(new LeaveUsageFilter { Year = 1999 }));
        Assert.Equal(2037, (await LeaveUsage(s, Hr).GetAsync(new LeaveUsageFilter())).Year); // mặc định năm của đồng hồ
    }
}
