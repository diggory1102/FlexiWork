using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Attendance;
using FW.Application.Features.Timesheets;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.IntegrationTests.Notifications;

namespace FW.IntegrationTests.Attendance;

/// <summary>
/// Quản lý / HR điều chỉnh giờ ra khi nhân viên quên check-out (Task 6.1) trên MySQL thật: tính lại giờ làm và phút về sớm, ghi người điều chỉnh,
/// báo nhân viên, chốt chặn bảng công đã khóa, phạm vi theo chi nhánh. Dữ liệu riêng ở tháng 5/2039. Seed: người dùng 2 = Quản lý chi nhánh 1, 4 = HR.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AttendanceAdjustmentServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, ManagerId = 2, HrId = 4;
    private static readonly DateTime WorkDate = new(2039, 5, 2);

    private readonly DatabaseFixture _db;

    public AttendanceAdjustmentServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class TestUser : ICurrentUserService
    {
        private readonly string[] _roles;

        public TestUser(int userId, int? branchId, params string[] roles)
        {
            UserId = userId;
            BranchId = branchId;
            _roles = roles;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => _roles.Contains(role);
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => new(2039, 5, 10, 20, 0, 0);
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private static readonly TestUser Manager1 = new(ManagerId, Branch1, RoleNames.Manager);
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);

    private static AttendanceAdjustmentService Adjuster(DbSession s, ICurrentUserService user, RecordingNotifier? notes = null) => new(
        new AttendanceLogRepository(s), new ScheduleRepository(s), new ShiftRepository(s), new UserRepository(s), notes ?? new RecordingNotifier(),
        new TimesheetGuard(new TimesheetRepository(s)), s, user, new FixedClock());

    private static AttendanceLogService Reader(DbSession s, ICurrentUserService user) =>
        new(new AttendanceLogRepository(s), new RecordingNotifier(), new TimesheetGuard(new TimesheetRepository(s)), user, new FixedClock());

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId)
    {
        var code = $"J{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    /// <summary>Ca 08:00–17:00 (nghỉ 60 phút) + lượt chấm công vào 08:05 CHƯA có giờ ra.</summary>
    private static async Task<(AttendanceLog Log, Schedule Schedule)> NewOpenLogAsync(DbSession s, int userId, int branchId, string status = AttendanceStatus.Valid,
        bool withSchedule = true)
    {
        var shift = new Shift
        {
            ShiftCode = $"AJ{Guid.NewGuid():N}"[..14], Name = "Ca quên check-out", BranchId = branchId, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), BreakMinutes = 60
        };
        await new ShiftRepository(s).AddAsync(shift);
        var schedule = new Schedule
        {
            UserId = userId, ShiftId = shift.Id, BranchId = branchId, WorkDate = WorkDate, Status = ScheduleStatus.InProgress, IsPublished = true, PublishedAt = WorkDate.AddDays(-3)
        };
        await new ScheduleRepository(s).AddAsync(schedule);

        var log = new AttendanceLog
        {
            ScheduleId = withSchedule ? schedule.Id : null, UserId = userId, BranchId = branchId, WorkDate = WorkDate,
            CheckinTime = WorkDate.AddHours(8).AddMinutes(5), CheckinImageUrl = "https://img.test/in.jpg", CheckinLat = 21.0316m, CheckinLng = 105.7831m,
            CheckinDistance = 12.3, CheckinWifiMatched = true, CheckinFaceMatched = true, CheckinFaceScore = 0.71, Status = status
        };
        await new AttendanceLogRepository(s).AddAsync(log);
        return (log, schedule);
    }

    private static AdjustCheckoutRequest Request(DateTime time, string? note = "Quên check-out, ca trưởng xác nhận về lúc 16:30") =>
        new() { CheckoutTime = time, Note = note };

    [SkippableFact]
    public async Task Manager_records_the_missing_checkout_and_the_employee_is_told()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s, Branch1);
        var (log, schedule) = await NewOpenLogAsync(s, employee.Id, Branch1);
        var notes = new RecordingNotifier();

        // Trước khi chỉnh: Quản lý thấy nút điều chỉnh, chính chủ thì không
        Assert.True((await Reader(s, Manager1).GetByIdAsync(log.Id)).CanAdjustCheckout);
        Assert.False((await Reader(s, new TestUser(employee.Id, Branch1, RoleNames.Employee)).GetByIdAsync(log.Id)).CanAdjustCheckout);

        var result = await Adjuster(s, Manager1, notes).AdjustCheckoutAsync(log.Id, Request(WorkDate.AddHours(16).AddMinutes(30)));

        // Giờ làm và phút về sớm tính như check-out thường: có mặt 08:05–16:30 = 505 phút × 480/540 = 7,48 giờ; về sớm 30 phút
        Assert.Equal((WorkDate.AddHours(16).AddMinutes(30), 30, 7.48m), (result.Log.CheckoutTime, result.Log.EarlyMinutes, result.Log.ActualWorkHours));
        Assert.Equal((ManagerId, true), (result.CheckoutAdjustedBy, result.CheckoutAdjustedAt.HasValue));
        Assert.False(string.IsNullOrWhiteSpace(result.CheckoutAdjusterName));
        Assert.Equal("Quên check-out, ca trưởng xác nhận về lúc 16:30", result.CheckoutAdjustNote);
        Assert.Null(result.CheckOut!.ImageUrl); // không có ảnh / vị trí minh chứng — giao diện ghi rõ là nhập thủ công
        Assert.False(result.CanAdjustCheckout);

        // Ca được đánh dấu hoàn thành như check-out thường
        Assert.Equal(ScheduleStatus.Completed, (await new ScheduleRepository(s).GetByIdAsync(schedule.Id))!.Status);

        // Nhân viên nhận thông báo kèm mã lượt chấm công
        var sent = Assert.Single(notes.To(employee.Id));
        Assert.Equal((NotificationType.Attendance, log.Id.ToString()), (sent.Type, sent.Reference));
        Assert.Contains("16:30", sent.Content);

        // Đã có giờ ra: không chỉnh lại được
        await Assert.ThrowsAsync<BadRequestException>(() => Adjuster(s, Manager1).AdjustCheckoutAsync(log.Id, Request(WorkDate.AddHours(17))));
        Assert.False((await Reader(s, Manager1).GetByIdAsync(log.Id)).CanAdjustCheckout);
    }

    [SkippableFact]
    public async Task Checkout_time_and_reason_are_validated()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s, Branch1);
        var (log, _) = await NewOpenLogAsync(s, employee.Id, Branch1);
        var adjuster = Adjuster(s, Manager1);

        await Assert.ThrowsAsync<BadRequestException>(() => adjuster.AdjustCheckoutAsync(log.Id, new AdjustCheckoutRequest { CheckoutTime = null, Note = "x" })); // thiếu giờ
        await Assert.ThrowsAsync<BadRequestException>(() => adjuster.AdjustCheckoutAsync(log.Id, Request(WorkDate.AddHours(16), note: "   "))); // thiếu lý do
        await Assert.ThrowsAsync<BadRequestException>(() => adjuster.AdjustCheckoutAsync(log.Id, Request(WorkDate.AddHours(8).AddMinutes(5)))); // bằng giờ vào
        await Assert.ThrowsAsync<BadRequestException>(() => adjuster.AdjustCheckoutAsync(log.Id, Request(WorkDate.AddHours(7)))); // trước giờ vào
        await Assert.ThrowsAsync<BadRequestException>(() => adjuster.AdjustCheckoutAsync(log.Id, Request(new DateTime(2039, 5, 11, 9, 0, 0)))); // tương lai (đồng hồ 10/5 20:00)
        await Assert.ThrowsAsync<BadRequestException>(() => adjuster.AdjustCheckoutAsync(log.Id, Request(WorkDate.AddDays(1).AddHours(5).AddMinutes(1)))); // quá 12 giờ sau khi ca kết thúc
        await Assert.ThrowsAsync<NotFoundException>(() => adjuster.AdjustCheckoutAsync(-1, Request(WorkDate.AddHours(16))));

        // Sau các lần bị từ chối, bản ghi không đổi; giờ ra sát mốc 12 giờ vẫn được chấp nhận
        Assert.Null((await new AttendanceLogRepository(s).GetByIdAsync(log.Id))!.CheckoutTime);
        var late = await adjuster.AdjustCheckoutAsync(log.Id, Request(WorkDate.AddDays(1).AddHours(5)));
        Assert.Equal(0, late.Log.EarlyMinutes); // ra sau giờ kết thúc ca: không về sớm
        Assert.Equal(7.93m, late.Log.ActualWorkHours); // 08:05–17:00 = 535 phút × 480/540: giờ làm chỉ tính trong khung ca
    }

    [SkippableFact]
    public async Task Only_the_right_people_can_adjust_and_rejected_or_orphan_logs_cannot()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var branch1Employee = await NewEmployeeAsync(s, Branch1);
        var branch2Employee = await NewEmployeeAsync(s, Branch2);
        var (log1, _) = await NewOpenLogAsync(s, branch1Employee.Id, Branch1);
        var (log2, _) = await NewOpenLogAsync(s, branch2Employee.Id, Branch2);
        var (rejected, _) = await NewOpenLogAsync(s, (await NewEmployeeAsync(s, Branch1)).Id, Branch1, AttendanceStatus.Invalid);
        var (orphan, _) = await NewOpenLogAsync(s, (await NewEmployeeAsync(s, Branch1)).Id, Branch1, withSchedule: false);
        var time = WorkDate.AddHours(16);

        // Quản lý chi nhánh 1 không chỉnh được chi nhánh 2; nhân viên không chỉnh được; không tự chỉnh bản ghi của mình
        await Assert.ThrowsAsync<ForbiddenException>(() => Adjuster(s, Manager1).AdjustCheckoutAsync(log2.Id, Request(time)));
        await Assert.ThrowsAsync<ForbiddenException>(() => Adjuster(s, new TestUser(branch1Employee.Id, Branch1, RoleNames.Employee)).AdjustCheckoutAsync(log1.Id, Request(time)));
        var manager = new User { EmployeeCode = $"M{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Email = $"{Guid.NewGuid():N}@test.flexiwork.vn", PasswordHash = "x", FullName = "QL tự chỉnh", BranchId = Branch1 };
        await new UserRepository(s).AddAsync(manager);
        var (ownLog, _) = await NewOpenLogAsync(s, manager.Id, Branch1);
        await Assert.ThrowsAsync<ForbiddenException>(() => Adjuster(s, new TestUser(manager.Id, Branch1, RoleNames.Manager)).AdjustCheckoutAsync(ownLog.Id, Request(time)));
        Assert.False((await Reader(s, new TestUser(manager.Id, Branch1, RoleNames.Manager)).GetByIdAsync(ownLog.Id)).CanAdjustCheckout);

        // Lượt bị từ chối và lượt mất lịch (không tính được giờ) không điều chỉnh được; cờ cũng tắt
        await Assert.ThrowsAsync<BadRequestException>(() => Adjuster(s, Manager1).AdjustCheckoutAsync(rejected.Id, Request(time)));
        await Assert.ThrowsAsync<BadRequestException>(() => Adjuster(s, Manager1).AdjustCheckoutAsync(orphan.Id, Request(time)));
        Assert.False((await Reader(s, Manager1).GetByIdAsync(rejected.Id)).CanAdjustCheckout);
        Assert.False((await Reader(s, Manager1).GetByIdAsync(orphan.Id)).CanAdjustCheckout);

        // HR chỉnh được mọi chi nhánh; lượt đang chờ duyệt vẫn chờ duyệt sau khi có giờ ra
        var (pending, _) = await NewOpenLogAsync(s, branch2Employee.Id, Branch2, AttendanceStatus.PendingApproval);
        Assert.Equal(AttendanceStatus.PendingApproval, (await Adjuster(s, Hr).AdjustCheckoutAsync(pending.Id, Request(time))).Log.Status);
        Assert.NotNull((await Adjuster(s, Hr).AdjustCheckoutAsync(log2.Id, Request(time))).Log.CheckoutTime);
    }

    [SkippableFact]
    public async Task A_locked_timesheet_blocks_the_adjustment()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s, Branch1);
        var (log, _) = await NewOpenLogAsync(s, employee.Id, Branch1);
        await new TimesheetRepository(s).AddAsync(new MonthlyTimesheet
        {
            UserId = employee.Id, BranchId = Branch1, Month = 5, Year = 2039, StandardWorkDays = 26, Status = TimesheetStatus.Locked
        });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Adjuster(s, Manager1).AdjustCheckoutAsync(log.Id, Request(WorkDate.AddHours(16))));
        Assert.Contains("05/2039", ex.Message);
        Assert.Null((await new AttendanceLogRepository(s).GetByIdAsync(log.Id))!.CheckoutTime); // không ghi gì khi bị chặn
        Assert.Null((await Reader(s, Manager1).GetByIdAsync(log.Id)).CheckoutAdjustedBy);
    }
}
