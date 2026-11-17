using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Payroll;
using FW.Application.Features.Scheduling;
using FW.Application.Features.Timesheets;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.IntegrationTests.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Payroll;

/// <summary>
/// Bảng lương (Task 5.5) trên MySQL thật: tính phiếu lương từ bảng công đã khóa + cấu hình lương, chốt kỳ (bảng công → đã chốt), gửi email,
/// xem phiếu cá nhân. Đồng hồ cố định 10/06/2036; mỗi test dùng một tháng riêng (2036-01…04) vì kỳ lương tính cho cả doanh nghiệp.
/// Người dùng 4 = HR. Seed: phụ cấp 1 = ca đêm (áp dụng cho tất cả), 3 = ăn trưa (gán riêng); chức vụ 4 có đơn giá giờ 35.000đ.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PayrollServiceTests
{
    private const int HrId = 4, StaffPosition = 4;
    private static readonly DateTime Now = new(2036, 6, 10, 10, 0, 0);

    private readonly DatabaseFixture _db;

    public PayrollServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => PayrollServiceTests.Now;
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private sealed class TestUser : ICurrentUserService
    {
        private readonly string[] _roles;

        public TestUser(int userId, params string[] roles)
        {
            UserId = userId;
            _roles = roles.Length == 0 ? new[] { RoleNames.Employee } : roles;
        }

        public int? UserId { get; }
        public int? BranchId => 1;
        public IReadOnlyList<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => _roles.Contains(role);
    }

    private sealed class CapturingEmail : IEmailService
    {
        public HashSet<string> FailFor { get; } = new();
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (FailFor.Contains(toEmail)) throw new InvalidOperationException("SMTP không phản hồi");
            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private static readonly TestUser Hr = new(HrId, RoleNames.Hr);

    private static PayrollConfigService Config(DbSession s) => new(
        new AllowanceRepository(s), new UserAllowanceRepository(s), new PayrollConfigRepository(s), new SystemSettingRepository(s),
        new AttendancePolicyService(new ViolationRuleRepository(s), new SystemSettingRepository(s), s), s, Hr, NullLogger<PayrollConfigService>.Instance);

    private static PayrollService Payroll(DbSession s, CapturingEmail? email = null, RecordingNotifier? notes = null) => new(
        new PayrollPeriodRepository(s), new PayslipRepository(s), new TimesheetRepository(s), new PayrollConfigRepository(s), new AllowanceRepository(s),
        new UserAllowanceRepository(s), Config(s), email ?? new CapturingEmail(), notes ?? new RecordingNotifier(), s, Hr, new FixedClock(),
        NullLogger<PayrollService>.Instance);

    private static PayslipService Slips(DbSession s, ICurrentUserService user) => new(new PayslipRepository(s), user);

    // ------------------------------------------------------------------ dữ liệu

    private static async Task<User> NewEmployeeAsync(DbSession s, string type = EmploymentType.FullTime, decimal? salary = 10_400_000m, int? positionId = StaffPosition)
    {
        var code = $"L{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Nhân viên {code}", BranchId = 1,
            PositionId = positionId, EmploymentType = type, BaseSalary = salary
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    /// <summary>Tạo bảng công của tháng; <paramref name="lockIt"/> = xác nhận rồi khóa (như quy trình thật).</summary>
    private static async Task<MonthlyTimesheet> SheetAsync(DbSession s, User user, int month, Action<MonthlyTimesheet> configure, bool lockIt = true, int year = 2036)
    {
        var sheet = new MonthlyTimesheet
        {
            UserId = user.Id, BranchId = user.BranchId, Year = year, Month = month, StandardWorkDays = 26, ScheduledShifts = 20, WorkedShifts = 20, CalculatedAt = Now
        };
        configure(sheet);
        var repo = new TimesheetRepository(s);
        await repo.UpsertDraftAsync(sheet);
        var saved = (await repo.GetByUsersAsync(year, month, new[] { user.Id })).Single();
        if (lockIt)
        {
            Assert.True(await repo.TryConfirmAsync(saved.Id, HrId, Now));
            Assert.Equal(1, await repo.LockConfirmedAsync(year, month, null, new[] { user.Id }, HrId, Now));
        }
        return saved;
    }

    private static void Work(MonthlyTimesheet t, decimal days = 20, decimal hours = 160, decimal paidLeave = 0, decimal ot = 0, decimal penalty = 0, int violations = 0,
        int nights = 0)
    {
        t.ActualWorkDays = days;
        t.ActualWorkHours = hours;
        t.PaidLeaveDays = paidLeave;
        t.TotalOvertimeHours = ot;
        t.PenaltyAmount = penalty;
        t.ViolationCount = violations;
        t.TotalLateMinutes = violations * 15;
        t.NightShifts = nights;
    }

    private static CalculatePayrollRequest Month(int month) => new() { Year = 2036, Month = month };

    // ------------------------------------------------------------------ Tính

    [SkippableFact]
    public async Task Calculation_turns_locked_timesheets_and_pay_config_into_payslips()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var payroll = Payroll(s);
        var settings = await Config(s).GetSettingsAsync();
        var catalog = await new AllowanceRepository(s).GetListAsync(activeOnly: true);
        var night = catalog.First(a => a.Name == "Phụ cấp ca đêm");
        var lunch = catalog.First(a => a.Name == "Phụ cấp ăn trưa");

        var fullTime = await NewEmployeeAsync(s);
        var partTime = await NewEmployeeAsync(s, EmploymentType.PartTime, salary: null);
        var unconfigured = await NewEmployeeAsync(s, salary: null);
        var notLocked = await NewEmployeeAsync(s);
        await new UserAllowanceRepository(s).ReplaceForUserAsync(fullTime.Id, new[] { new UserAllowance { AllowanceId = lunch.Id } });

        var sheetFull = await SheetAsync(s, fullTime, 1, t => Work(t, days: 20, hours: 160, paidLeave: 2, ot: 4, penalty: 70000, violations: 2, nights: 5));
        await SheetAsync(s, partTime, 1, t => Work(t, days: 10, hours: 80));
        await SheetAsync(s, unconfigured, 1, t => Work(t));
        await SheetAsync(s, notLocked, 1, t => Work(t), lockIt: false);

        var result = await payroll.CalculateAsync(Month(1));
        Assert.Equal((2, 1, 1), (result.Calculated, result.Skipped.Count, result.UnlockedTimesheets));
        Assert.Equal(unconfigured.Id, result.Skipped.Single().UserId);
        Assert.Contains("lương cơ bản", result.Skipped.Single().Reason);
        Assert.Equal((PayrollPeriodStatus.Draft, 2), (result.Period.Status, result.Period.EmployeeCount));
        Assert.Equal(settings.StandardWorkDays, result.Period.StandardWorkDays);
        Assert.Equal(settings.OvertimeMultiplier, result.Period.OvertimeMultiplier);

        var slips = await payroll.GetPayslipsAsync(result.Period.Id, new PayslipFilter { PageSize = 50 });
        Assert.Equal(2, slips.TotalCount);
        var full = Assert.Single(slips.Items, p => p.Employee.Id == fullTime.Id);
        var std = settings.StandardWorkDays;
        var basic = Math.Round(10_400_000m * 20 / std, 0) + Math.Round(10_400_000m * 2 / std, 0); // 20 công + 2 ngày phép có lương
        var ot = Math.Round(4 * Math.Round(10_400_000m / std / 8m, 2) * settings.OvertimeMultiplier, 0);
        var allowances = 5 * night.Amount + lunch.Amount;
        Assert.Equal((basic, ot, allowances, 70000m), (full.BasicPay, full.OvertimePay, full.AllowanceTotal, full.PenaltyTotal));
        Assert.Equal(basic + allowances + ot - 70000m, full.NetPay);
        Assert.Equal((EmploymentType.FullTime, 10_400_000m, PayslipStatus.Draft), (full.EmploymentType, full.BaseSalary, full.Status));

        var detail = await Slips(s, Hr).GetDetailAsync(full.Id);
        Assert.Equal(full.NetPay, detail.Items.Sum(i => i.Amount)); // các dòng cộng lại đúng thực nhận
        Assert.Contains(detail.Items, i => i is { Kind: PayslipItemKind.Penalty, Amount: -70000m });
        Assert.Contains(detail.Items, i => i.Name == night.Name && i.Quantity == 5 && i.Amount == 5 * night.Amount);
        Assert.Contains(detail.Items, i => i is { Kind: PayslipItemKind.Allowance } && i.Name == lunch.Name);

        var part = Assert.Single(slips.Items, p => p.Employee.Id == partTime.Id);
        Assert.Equal((EmploymentType.PartTime, 2_800_000m), (part.EmploymentType, part.BasicPay)); // 80 giờ × 35.000

        // Lương quy đổi tạm tính được ghi lại vào bảng công (kể cả bảng đã khóa)
        var stored = (await new TimesheetRepository(s).GetByUsersAsync(2036, 1, new[] { fullTime.Id })).Single();
        Assert.Equal((sheetFull.Id, full.NetPay), (stored.Id, stored.CalculatedSalary));
        Assert.Equal(TimesheetStatus.Locked, stored.Status);
    }

    [SkippableFact]
    public async Task Recalculating_replaces_the_draft_and_follows_pay_config_changes()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var payroll = Payroll(s);
        var employee = await NewEmployeeAsync(s, salary: 10_400_000m);
        await SheetAsync(s, employee, 2, t => Work(t, days: 26));

        var first = await payroll.CalculateAsync(Month(2));
        var firstSlip = Assert.Single((await payroll.GetPayslipsAsync(first.Period.Id, new PayslipFilter { Search = employee.EmployeeCode })).Items);

        await new PayrollConfigRepository(s).UpdatePayAsync(employee.Id, 13_000_000m, null);
        var second = await payroll.CalculateAsync(Month(2));
        Assert.Equal(first.Period.Id, second.Period.Id); // cùng một kỳ
        var secondSlip = Assert.Single((await payroll.GetPayslipsAsync(second.Period.Id, new PayslipFilter { Search = employee.EmployeeCode })).Items);
        Assert.NotEqual(firstSlip.Id, secondSlip.Id);                 // phiếu nháp được thay mới, không nhân đôi
        Assert.Equal(13_000_000m, secondSlip.BaseSalary);
        Assert.True(secondSlip.NetPay > firstSlip.NetPay);
        Assert.Equal(1, (await payroll.GetPayslipsAsync(second.Period.Id, new PayslipFilter { Search = employee.EmployeeCode })).TotalCount);

        await Assert.ThrowsAsync<BadRequestException>(() => payroll.CalculateAsync(new CalculatePayrollRequest { Year = 2036, Month = 7 })); // tháng chưa tới
        await Assert.ThrowsAsync<BadRequestException>(() => payroll.CalculateAsync(new CalculatePayrollRequest { Year = 2036, Month = 13 }));
    }

    // ------------------------------------------------------------------ Chốt

    [SkippableFact]
    public async Task Finalizing_is_blocked_until_the_month_is_over_all_sheets_are_locked_and_pay_is_configured()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var payroll = Payroll(s);

        // Tháng hiện tại (6/2036) chưa kết thúc
        var current = await payroll.CalculateAsync(Month(6));
        var tooEarly = await Assert.ThrowsAsync<BadRequestException>(() => payroll.FinalizeAsync(current.Period.Id));
        Assert.Contains("chưa kết thúc", tooEarly.Message);
        Assert.False((await payroll.GetPeriodAsync(current.Period.Id)).Readiness.CanFinalize);

        // Tháng 3: còn bảng công chưa khóa + nhân viên chưa cấu hình lương
        var ok = await NewEmployeeAsync(s);
        var unconfigured = await NewEmployeeAsync(s, salary: null);
        var draft = await NewEmployeeAsync(s);
        await SheetAsync(s, ok, 3, t => Work(t));
        await SheetAsync(s, unconfigured, 3, t => Work(t));
        await SheetAsync(s, draft, 3, t => Work(t), lockIt: false);
        var march = await payroll.CalculateAsync(Month(3));

        var readiness = (await payroll.GetPeriodAsync(march.Period.Id)).Readiness;
        Assert.Equal((true, 2, 1, 1, false), (readiness.MonthEnded, readiness.LockedTimesheets, readiness.UnlockedTimesheets, readiness.MissingPayslips, readiness.CanFinalize));

        var blocked = await Assert.ThrowsAsync<BadRequestException>(() => payroll.FinalizeAsync(march.Period.Id));
        Assert.Contains(blocked.Errors!, e => e.Contains("chưa khóa"));
        Assert.Contains(blocked.Errors!, e => e.Contains(unconfigured.EmployeeCode));
        Assert.Equal(PayrollPeriodStatus.Draft, (await payroll.GetPeriodAsync(march.Period.Id)).Period.Status); // lỗi thì không đổi gì
        Assert.Equal(TimesheetStatus.Locked, (await new TimesheetRepository(s).GetByUsersAsync(2036, 3, new[] { ok.Id })).Single().Status);
    }

    [SkippableFact]
    public async Task Finalizing_freezes_the_period_and_timesheets_and_notifies_employees()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var notes = new RecordingNotifier();
        var payroll = Payroll(s, notes: notes);
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s, EmploymentType.PartTime, salary: null);
        await SheetAsync(s, a, 4, t => Work(t, days: 26));
        await SheetAsync(s, b, 4, t => Work(t, days: 5, hours: 40));

        var period = (await payroll.CalculateAsync(Month(4))).Period;
        Assert.True((await payroll.GetPeriodAsync(period.Id)).Readiness.CanFinalize);

        // Trước khi chốt nhân viên chưa thấy phiếu của mình
        var firstSlip = Assert.Single((await payroll.GetPayslipsAsync(period.Id, new PayslipFilter { Search = a.EmployeeCode })).Items);
        var asA = Slips(s, new TestUser(a.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => asA.GetDetailAsync(firstSlip.Id));
        Assert.Equal(0, (await asA.GetMineAsync(new MyPayslipFilter { Year = 2036 })).TotalCount);

        // Đổi cấu hình lương sau khi tính: chốt phải tính lại lần cuối nên phiếu khớp cấu hình mới
        await new PayrollConfigRepository(s).UpdatePayAsync(a.Id, 13_000_000m, null);
        var done = await payroll.FinalizeAsync(period.Id);
        Assert.Equal(2, done.Payslips);
        Assert.Equal(PayrollPeriodStatus.Finalized, done.Period.Status);
        Assert.Equal(HrId, (await new PayrollPeriodRepository(s).GetRowAsync(period.Id))!.FinalizedBy);
        var slipA = Assert.Single((await payroll.GetPayslipsAsync(period.Id, new PayslipFilter { Search = a.EmployeeCode })).Items);
        Assert.Equal(13_000_000m, slipA.BaseSalary);

        // Bảng công → đã chốt; từ đây tháng này bị khóa hẳn
        var sheets = await new TimesheetRepository(s).GetByUsersAsync(2036, 4, new[] { a.Id, b.Id });
        Assert.All(sheets, t => Assert.Equal(TimesheetStatus.Finalized, t.Status));
        Assert.True(await new TimesheetRepository(s).IsFrozenAsync(a.Id, 2036, 4));
        await Assert.ThrowsAsync<BadRequestException>(() => payroll.CalculateAsync(Month(4)));
        await Assert.ThrowsAsync<BadRequestException>(() => payroll.FinalizeAsync(period.Id));

        // Thông báo tới cả hai nhân viên, tham chiếu mã kỳ lương
        var note = Assert.Single(notes.Items, n => n.Type == NotificationType.Payslip);
        Assert.Equal(new[] { a.Id, b.Id }.Order(), note.Users.Order());
        Assert.Equal(period.Id.ToString(), note.Reference);

        // Sau khi chốt: chính chủ xem được phiếu của mình, người khác thì không
        var detail = await asA.GetDetailAsync(slipA.Id);
        Assert.Equal(slipA.NetPay, detail.Payslip.NetPay);
        Assert.Equal(1, (await asA.GetMineAsync(new MyPayslipFilter { Year = 2036 })).TotalCount);
        Assert.Equal(0, (await asA.GetMineAsync(new MyPayslipFilter { Year = 2035 })).TotalCount);
        await Assert.ThrowsAsync<NotFoundException>(() => Slips(s, new TestUser(b.Id)).GetDetailAsync(slipA.Id));
        Assert.Equal(slipA.Id, (await Slips(s, Hr).GetDetailAsync(slipA.Id)).Payslip.Id); // HR xem được mọi phiếu
    }

    // ------------------------------------------------------------------ Gửi phiếu lương

    [SkippableFact]
    public async Task Payslips_are_emailed_only_after_finalizing_and_failures_can_be_retried()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var email = new CapturingEmail();
        var payroll = Payroll(s, email);
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s);
        await SheetAsync(s, a, 5, t => Work(t, days: 26));
        await SheetAsync(s, b, 5, t => Work(t, days: 13));
        var period = (await payroll.CalculateAsync(Month(5))).Period;

        await Assert.ThrowsAsync<BadRequestException>(() => payroll.SendAsync(period.Id, new SendPayslipsRequest())); // chưa chốt
        await payroll.FinalizeAsync(period.Id);

        // b gửi lỗi, a thành công
        email.FailFor.Add(b.Email);
        var first = await payroll.SendAsync(period.Id, new SendPayslipsRequest());
        Assert.Equal((1, 1), (first.Sent, first.Failed));
        Assert.Equal(b.Id, first.Failures.Single().UserId);
        var mail = Assert.Single(email.Sent);
        Assert.Equal((a.Email, "[FlexiWork] Phiếu lương tháng 05/2036"), (mail.To, mail.Subject));
        Assert.Contains(a.EmployeeCode, mail.Body);
        Assert.Contains("Thực nhận", mail.Body);
        Assert.Contains("10.400.000", mail.Body);

        var all = await payroll.GetPayslipsAsync(period.Id, new PayslipFilter { PageSize = 50 });
        Assert.Equal(PayslipStatus.Sent, all.Items.Single(p => p.Employee.Id == a.Id).Status);
        Assert.NotNull(all.Items.Single(p => p.Employee.Id == a.Id).SentAt);
        Assert.Equal(PayslipStatus.Draft, all.Items.Single(p => p.Employee.Id == b.Id).Status);
        Assert.Equal(1, (await payroll.GetPeriodAsync(period.Id)).Period.SentCount);
        Assert.Single((await payroll.GetPayslipsAsync(period.Id, new PayslipFilter { Status = PayslipStatus.Draft })).Items);
        await Assert.ThrowsAsync<BadRequestException>(() => payroll.GetPayslipsAsync(period.Id, new PayslipFilter { Status = "paid" }));

        // Gửi lại: chỉ phiếu chưa gửi (không gửi trùng cho a)
        email.FailFor.Clear();
        var retry = await payroll.SendAsync(period.Id, new SendPayslipsRequest());
        Assert.Equal((1, 0), (retry.Sent, retry.Failed));
        Assert.Equal(2, email.Sent.Count);
        Assert.Equal(b.Email, email.Sent.Last().To);

        // Hết phiếu chưa gửi; gửi lại một phiếu cụ thể thì được; phiếu của kỳ khác bị từ chối
        Assert.Equal(0, (await payroll.SendAsync(period.Id, new SendPayslipsRequest())).Sent);
        var slipA = all.Items.Single(p => p.Employee.Id == a.Id);
        Assert.Equal(1, (await payroll.SendAsync(period.Id, new SendPayslipsRequest { PayslipIds = new List<long> { slipA.Id } })).Sent);
        await Assert.ThrowsAsync<BadRequestException>(() => payroll.SendAsync(period.Id, new SendPayslipsRequest { PayslipIds = new List<long> { 99_999_999 } }));
    }

    // ------------------------------------------------------------------ Kỳ lương

    [SkippableFact]
    public async Task Periods_are_listed_with_totals_and_unknown_ids_are_rejected()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var payroll = Payroll(s);

        await Assert.ThrowsAsync<NotFoundException>(() => payroll.GetPeriodAsync(99_999_999));
        await Assert.ThrowsAsync<NotFoundException>(() => payroll.FinalizeAsync(99_999_999));
        await Assert.ThrowsAsync<NotFoundException>(() => payroll.SendAsync(99_999_999, new SendPayslipsRequest()));

        // Hai kỳ của năm 2035 (dữ liệu riêng) → danh sách mới nhất trước, tổng hợp khớp phiếu lương
        var employee = await NewEmployeeAsync(s);
        await SheetAsync(s, employee, 11, t => Work(t, days: 13), year: 2035);
        await SheetAsync(s, employee, 12, t => Work(t, days: 26), year: 2035);
        await payroll.CalculateAsync(new CalculatePayrollRequest { Year = 2035, Month = 11 });
        var december = (await payroll.CalculateAsync(new CalculatePayrollRequest { Year = 2035, Month = 12 })).Period;

        var periods = await payroll.GetPeriodsAsync(new PayrollPeriodFilter { Year = 2035 });
        Assert.Equal(new[] { 12, 11 }, periods.Select(p => p.Month));
        Assert.All(periods, p => Assert.Equal(2035, p.Year));

        var detail = await payroll.GetPeriodAsync(december.Id);
        Assert.Equal((december.TotalNet, 1), (detail.Totals.Net, detail.Totals.Count));
        Assert.Equal(10_400_000m, detail.Totals.Basic); // 26 công × lương tháng ÷ công chuẩn
        Assert.Equal((true, 1, 0, 0, true), (detail.Readiness.MonthEnded, detail.Readiness.LockedTimesheets, detail.Readiness.UnlockedTimesheets,
            detail.Readiness.MissingPayslips, detail.Readiness.CanFinalize));
    }
}
