using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Repositories;
using MySqlConnector;

namespace FW.IntegrationTests.Repositories;

/// <summary>
/// Repository nhật ký chấm công (Task 3.1) trên MySQL thật: minh chứng 3 lớp lượt vào/ra, mỗi ca một bản ghi,
/// lượt đang mở, phát hiện một thiết bị chấm công cho nhiều người.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AttendanceLogRepositoryTests
{
    // Dữ liệu seed: chi nhánh 1; người dùng 2 (Quản lý), 3 (Nhân viên)
    private const int BranchId = 1;
    private const int ManagerId = 2;
    private const int EmployeeId = 3;

    private readonly DatabaseFixture _db;

    public AttendanceLogRepositoryTests(DatabaseFixture db)
    {
        _db = db;
    }

    private static async Task<Schedule> NewScheduleAsync(DbSessionScope scope, int userId, DateTime workDate)
    {
        var shift = new Shift
        {
            ShiftCode = $"AT-{Guid.NewGuid():N}"[..20],
            Name = "Ca chấm công thử",
            BranchId = BranchId,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(17),
            BreakMinutes = 60
        };
        await scope.Shifts.AddAsync(shift);
        var schedule = new Schedule
        {
            UserId = userId, ShiftId = shift.Id, BranchId = BranchId, WorkDate = workDate,
            IsPublished = true, PublishedAt = workDate.AddDays(-3)
        };
        await scope.Schedules.AddAsync(schedule);
        return schedule;
    }

    private static AttendanceLog CheckIn(Schedule schedule, DateTime time, string deviceId) => new()
    {
        ScheduleId = schedule.Id,
        UserId = schedule.UserId,
        BranchId = schedule.BranchId,
        WorkDate = schedule.WorkDate,
        CheckinTime = time,
        CheckinImageUrl = "https://res.cloudinary.com/demo/image/upload/attendance/in.jpg",
        CheckinLat = 21.03681234m,
        CheckinLng = 105.78251234m,
        CheckinAccuracy = 12.5,
        CheckinDistance = 18.37,
        CheckinBssid = "00:1A:2B:3C:4D:5E",
        CheckinWifiMatched = true,
        CheckinFaceMatched = true,
        CheckinFaceScore = 0.6731,
        CheckinDeviceId = deviceId,
        LateMinutes = 7
    };

    [SkippableFact]
    public async Task Check_in_and_check_out_evidence_round_trips()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var scope = new DbSessionScope(session);
        var logs = new AttendanceLogRepository(session);

        var workDate = new DateTime(2031, 3, 10);
        var schedule = await NewScheduleAsync(scope, EmployeeId, workDate);
        var log = CheckIn(schedule, workDate.AddHours(8).AddMinutes(7).AddSeconds(30), "device-A");
        var id = await logs.AddAsync(log);

        // Đang trong ca: lượt mở của nhân viên chính là bản ghi vừa tạo
        var open = await logs.GetOpenByUserAsync(EmployeeId);
        Assert.NotNull(open);
        Assert.Equal(id, open!.Id);
        Assert.False(open.IsCheckedOut);
        Assert.Equal(workDate, open.WorkDate);
        Assert.Equal(21.03681234m, open.CheckinLat);
        Assert.Equal(105.78251234m, open.CheckinLng);
        Assert.Equal(12.5, open.CheckinAccuracy);
        Assert.Equal(18.37, open.CheckinDistance, precision: 6);
        Assert.Equal("00:1A:2B:3C:4D:5E", open.CheckinBssid);
        Assert.True(open.CheckinWifiMatched);
        Assert.True(open.CheckinFaceMatched);
        Assert.Equal(0.6731, open.CheckinFaceScore!.Value, precision: 6);
        Assert.Equal(AttendanceStatus.Valid, open.Status);
        Assert.Equal(7, open.LateMinutes);

        // Check-out: Wi-Fi không khớp → chờ duyệt
        open.CheckoutTime = workDate.AddHours(16).AddMinutes(50);
        open.CheckoutImageUrl = "https://res.cloudinary.com/demo/image/upload/attendance/out.jpg";
        open.CheckoutLat = 21.0368m;
        open.CheckoutLng = 105.7825m;
        open.CheckoutDistance = 22.4;
        open.CheckoutBssid = "AA:BB:CC:DD:EE:FF";
        open.CheckoutWifiMatched = false;
        open.CheckoutFaceMatched = true;
        open.CheckoutFaceScore = 0.58;
        open.CheckoutDeviceId = "device-A";
        open.EarlyMinutes = 10;
        open.ActualWorkHours = 7.71m;
        open.Status = AttendanceStatus.PendingApproval;
        open.Note = "Wi-Fi lúc ra không khớp Wi-Fi nội bộ";
        Assert.True(await logs.UpdateAsync(open));

        Assert.Null(await logs.GetOpenByUserAsync(EmployeeId));
        var saved = await logs.GetByScheduleIdAsync(schedule.Id);
        Assert.NotNull(saved);
        Assert.True(saved!.IsCheckedOut);
        Assert.Equal(workDate.AddHours(16).AddMinutes(50), saved.CheckoutTime);
        Assert.Null(saved.CheckoutAccuracy);
        Assert.False(saved.CheckoutWifiMatched);
        Assert.Equal(7.71m, saved.ActualWorkHours);
        Assert.Equal(AttendanceStatus.PendingApproval, saved.Status);

        // Quản lý duyệt
        saved.Status = AttendanceStatus.Valid;
        saved.ReviewedBy = ManagerId;
        saved.ReviewedAt = workDate.AddHours(18);
        saved.ReviewNote = "Router dự phòng của chi nhánh";
        Assert.True(await logs.UpdateAsync(saved));
        var reviewed = await logs.GetByIdAsync(id);
        Assert.Equal(ManagerId, reviewed!.ReviewedBy);
        Assert.Equal(AttendanceStatus.Valid, reviewed.Status);
    }

    [SkippableFact]
    public async Task A_schedule_has_at_most_one_attendance_log()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var scope = new DbSessionScope(session);
        var logs = new AttendanceLogRepository(session);

        var workDate = new DateTime(2031, 3, 11);
        var schedule = await NewScheduleAsync(scope, EmployeeId, workDate);
        var first = CheckIn(schedule, workDate.AddHours(8), "device-B");
        first.CheckoutTime = workDate.AddHours(17); // đóng lượt để không ảnh hưởng test khác
        await logs.AddAsync(first);

        // Hai yêu cầu check-in đồng thời cho cùng một ca: CSDL chặn bản ghi thứ hai
        var duplicate = await Assert.ThrowsAsync<MySqlException>(() => logs.AddAsync(CheckIn(schedule, workDate.AddHours(8).AddSeconds(1), "device-B")));
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, duplicate.ErrorCode);
    }

    [SkippableFact]
    public async Task Detects_other_employees_checking_in_on_the_same_device()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var scope = new DbSessionScope(session);
        var logs = new AttendanceLogRepository(session);

        var workDate = new DateTime(2031, 3, 12);
        var device = $"shared-{Guid.NewGuid():N}";
        foreach (var userId in new[] { EmployeeId, ManagerId })
        {
            var schedule = await NewScheduleAsync(scope, userId, workDate);
            var log = CheckIn(schedule, workDate.AddHours(8), device);
            log.CheckoutTime = workDate.AddHours(17);
            await logs.AddAsync(log);
        }

        Assert.Equal(new[] { ManagerId }, await logs.GetOtherUsersOnDeviceAsync(device, workDate, EmployeeId));
        Assert.Equal(new[] { EmployeeId }, await logs.GetOtherUsersOnDeviceAsync(device, workDate, ManagerId));
        Assert.Empty(await logs.GetOtherUsersOnDeviceAsync(device, workDate.AddDays(1), EmployeeId)); // ngày khác
        Assert.Empty(await logs.GetOtherUsersOnDeviceAsync("thiet-bi-khac", workDate, EmployeeId));
    }

    /// <summary>Gom các repository phụ dùng để tạo dữ liệu ca/lịch.</summary>
    private sealed class DbSessionScope
    {
        public DbSessionScope(FW.Infrastructure.Database.DbSession session)
        {
            Shifts = new ShiftRepository(session);
            Schedules = new ScheduleRepository(session);
        }

        public ShiftRepository Shifts { get; }
        public ScheduleRepository Schedules { get; }
    }
}
