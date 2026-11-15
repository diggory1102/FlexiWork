using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Attendance;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.Attendance;

/// <summary>
/// Tra cứu & duyệt nhật ký chấm công (Task 3.3) trên MySQL thật: phạm vi theo vai trò, bộ lọc, chi tiết minh chứng,
/// quy tắc duyệt. Dữ liệu riêng ở tháng 5/2033. Seed: người dùng 2 = Quản lý chi nhánh 1, 4 = HR.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AttendanceLogServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, ManagerId = 2, HrId = 4;
    private static readonly DateOnly Day1 = new(2033, 5, 2), Day2 = new(2033, 5, 3);

    private readonly DatabaseFixture _db;

    public AttendanceLogServiceTests(DatabaseFixture db)
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
        public DateTime Now => new(2033, 5, 3, 20, 0, 0);
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private static AttendanceLogService Service(DbSession s, ICurrentUserService user) =>
        new(new AttendanceLogRepository(s), user, new FixedClock());

    private static readonly TestUser Manager1 = new(ManagerId, Branch1, RoleNames.Manager);
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);

    private static AttendanceLogFilter Range(int? branchId = null, string? status = null, string? search = null, int pageSize = 50) => new()
    {
        FromDate = Day1, ToDate = Day2, BranchId = branchId, Status = status, Search = search, PageSize = pageSize
    };

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId)
    {
        var code = $"L{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = $"Nhân viên {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    /// <summary>Ca 08:00–17:00 đã công bố + một lượt chấm công (đã check-out) của nhân viên vào ngày đó.</summary>
    private static async Task<AttendanceLog> NewLogAsync(DbSession s, int userId, int branchId, DateOnly day, string status, int checkinHour = 8)
    {
        var workDate = day.ToDateTime(TimeOnly.MinValue);
        var shift = new Shift
        {
            ShiftCode = $"LG{Guid.NewGuid():N}"[..14], Name = "Ca tra cứu", BranchId = branchId,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), BreakMinutes = 60
        };
        await new ShiftRepository(s).AddAsync(shift);
        var schedule = new Schedule
        {
            UserId = userId, ShiftId = shift.Id, BranchId = branchId, WorkDate = workDate,
            Status = ScheduleStatus.Completed, IsPublished = true, PublishedAt = workDate.AddDays(-3)
        };
        await new ScheduleRepository(s).AddAsync(schedule);

        var log = new AttendanceLog
        {
            ScheduleId = schedule.Id, UserId = userId, BranchId = branchId, WorkDate = workDate,
            CheckinTime = workDate.AddHours(checkinHour), CheckinImageUrl = "https://img.test/in.jpg",
            CheckinLat = 21.0316m, CheckinLng = 105.7831m, CheckinAccuracy = 7, CheckinDistance = 12.3,
            CheckinBssid = status == AttendanceStatus.PendingApproval ? null : "00:1A:2B:3C:4D:5E",
            CheckinWifiMatched = status != AttendanceStatus.PendingApproval, CheckinFaceMatched = true, CheckinFaceScore = 0.71,
            CheckinDeviceId = "dev-log",
            CheckoutTime = workDate.AddHours(17), CheckoutImageUrl = "https://img.test/out.jpg",
            CheckoutLat = 21.0316m, CheckoutLng = 105.7831m, CheckoutDistance = 9.8, CheckoutWifiMatched = true,
            CheckoutFaceMatched = true, CheckoutFaceScore = 0.69,
            ActualWorkHours = 8, Status = status,
            Note = status == AttendanceStatus.PendingApproval ? "Không xác định được Wi-Fi lúc vào." : null
        };
        await new AttendanceLogRepository(s).AddAsync(log);
        return log;
    }

    [SkippableFact]
    public async Task Logs_are_scoped_by_role_and_filterable()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewEmployeeAsync(s, Branch1);
        var b = await NewEmployeeAsync(s, Branch2);
        var a1 = await NewLogAsync(s, a.Id, Branch1, Day1, AttendanceStatus.Valid);
        var a2 = await NewLogAsync(s, a.Id, Branch1, Day2, AttendanceStatus.PendingApproval, checkinHour: 9);
        var b1 = await NewLogAsync(s, b.Id, Branch2, Day1, AttendanceStatus.Valid);

        // Nhân viên: chỉ thấy bản ghi của mình, mới nhất trước
        var mine = await Service(s, new TestUser(a.Id, Branch1, RoleNames.Employee)).GetMineAsync(Range(branchId: Branch2));
        Assert.Equal(new[] { a2.Id, a1.Id }, mine.Items.Select(i => i.Id));
        Assert.Equal(2, mine.TotalCount);

        // Quản lý chi nhánh 1: không thấy chi nhánh 2, không được chọn chi nhánh 2
        var manager = Service(s, Manager1);
        var branch1 = await manager.GetLogsAsync(Range());
        Assert.Contains(branch1.Items, i => i.Id == a1.Id);
        Assert.DoesNotContain(branch1.Items, i => i.Id == b1.Id);
        Assert.All(branch1.Items, i => Assert.Equal(Branch1, i.BranchId));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetLogsAsync(Range(branchId: Branch2)));

        // HR: mọi chi nhánh; lọc theo chi nhánh, trạng thái, mã nhân viên; phân trang
        var hr = Service(s, Hr);
        var all = await hr.GetLogsAsync(Range());
        Assert.Contains(all.Items, i => i.Id == a1.Id);
        Assert.Contains(all.Items, i => i.Id == b1.Id);
        Assert.All((await hr.GetLogsAsync(Range(branchId: Branch2))).Items, i => Assert.Equal(Branch2, i.BranchId));
        Assert.All((await hr.GetLogsAsync(Range(status: AttendanceStatus.PendingApproval))).Items,
            i => Assert.Equal(AttendanceStatus.PendingApproval, i.Status));

        var byCode = await hr.GetLogsAsync(Range(search: a.EmployeeCode.ToLowerInvariant(), pageSize: 1));
        Assert.Equal(2, byCode.TotalCount);
        Assert.Equal(2, byCode.TotalPages);
        var row = Assert.Single(byCode.Items);
        Assert.Equal(a2.Id, row.Id);
        Assert.Equal(a.FullName, row.FullName);
        Assert.Equal("FlexiWork Cầu Giấy", row.BranchName);
        Assert.Equal("Ca tra cứu", row.ShiftName);
        Assert.Equal("08:00", row.ShiftStartTime);
        Assert.Equal("17:00", row.ShiftEndTime);
        Assert.Equal(Day2.ToDateTime(TimeOnly.MinValue).AddHours(9), row.CheckinTime);
        Assert.False(row.CheckinWifiMatched);
        Assert.True(row.CheckoutWifiMatched);

        // Khoảng ngày / trạng thái không hợp lệ
        await Assert.ThrowsAsync<BadRequestException>(() => hr.GetLogsAsync(new AttendanceLogFilter { FromDate = Day2, ToDate = Day1 }));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.GetLogsAsync(new AttendanceLogFilter { FromDate = Day1, ToDate = Day1.AddDays(400) }));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.GetLogsAsync(Range(status: "late")));
    }

    [SkippableFact]
    public async Task Detail_shows_evidence_to_owner_and_branch_managers_only()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewEmployeeAsync(s, Branch1);
        var other = await NewEmployeeAsync(s, Branch1);
        var log = await NewLogAsync(s, a.Id, Branch1, Day1, AttendanceStatus.PendingApproval);

        var own = await Service(s, new TestUser(a.Id, Branch1, RoleNames.Employee)).GetByIdAsync(log.Id);
        Assert.False(own.CanReview);
        Assert.Equal(21.0316m, own.CheckIn.Latitude);
        Assert.Equal(7, own.CheckIn.AccuracyMeters);
        Assert.Null(own.CheckIn.Bssid);
        Assert.Equal(0.71, own.CheckIn.FaceScore);
        Assert.Equal("https://img.test/out.jpg", own.CheckOut!.ImageUrl);
        Assert.Equal(50, own.Branch.Radius);
        Assert.Equal(21.031584m, own.Branch.Latitude);

        Assert.True((await Service(s, Manager1).GetByIdAsync(log.Id)).CanReview);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, new TestUser(other.Id, Branch1, RoleNames.Employee)).GetByIdAsync(log.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, new TestUser(ManagerId, Branch2, RoleNames.Manager)).GetByIdAsync(log.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(s, Hr).GetByIdAsync(-1));
    }

    [SkippableFact]
    public async Task Pending_logs_are_approved_or_rejected_by_someone_else_in_scope()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewEmployeeAsync(s, Branch1);
        var b = await NewEmployeeAsync(s, Branch2);
        var pendingA = await NewLogAsync(s, a.Id, Branch1, Day1, AttendanceStatus.PendingApproval);
        var pendingA2 = await NewLogAsync(s, a.Id, Branch1, Day2, AttendanceStatus.PendingApproval);
        var pendingB = await NewLogAsync(s, b.Id, Branch2, Day1, AttendanceStatus.PendingApproval);
        var pendingManager = await NewLogAsync(s, ManagerId, Branch1, Day2, AttendanceStatus.PendingApproval);
        var valid = await NewLogAsync(s, a.Id, Branch1, Day2, AttendanceStatus.Valid, checkinHour: 13);

        var manager = Service(s, Manager1);

        // Chấp nhận: tính công, lưu người duyệt
        var approved = await manager.ReviewAsync(pendingA.Id, new ReviewAttendanceRequest { Approved = true, Note = "  Router dự phòng  " });
        Assert.Equal(AttendanceStatus.Valid, approved.Log.Status);
        Assert.Equal(ManagerId, approved.ReviewedBy);
        Assert.Equal("Trần Quản Lý", approved.ReviewerName);
        Assert.Equal(new DateTime(2033, 5, 3, 20, 0, 0), approved.ReviewedAt);
        Assert.Equal("Router dự phòng", approved.ReviewNote);
        Assert.False(approved.CanReview);

        // Không duyệt lại bản ghi đã xử lý / bản ghi hợp lệ
        await Assert.ThrowsAsync<BadRequestException>(() => manager.ReviewAsync(pendingA.Id, new ReviewAttendanceRequest { Approved = false, Note = "x" }));
        await Assert.ThrowsAsync<BadRequestException>(() => manager.ReviewAsync(valid.Id, new ReviewAttendanceRequest { Approved = true }));

        // Từ chối phải có lý do
        await Assert.ThrowsAsync<BadRequestException>(() => manager.ReviewAsync(pendingA2.Id, new ReviewAttendanceRequest { Approved = false, Note = " " }));
        var rejected = await manager.ReviewAsync(pendingA2.Id, new ReviewAttendanceRequest { Approved = false, Note = "Không có mặt tại cửa hàng" });
        Assert.Equal(AttendanceStatus.Invalid, rejected.Log.Status);

        // Ngoài chi nhánh / tự duyệt của mình → cấm; bản ghi của Quản lý do HR duyệt
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.ReviewAsync(pendingB.Id, new ReviewAttendanceRequest { Approved = true }));
        Assert.False((await manager.GetByIdAsync(pendingManager.Id)).CanReview);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.ReviewAsync(pendingManager.Id, new ReviewAttendanceRequest { Approved = true }));
        Assert.True((await Service(s, Hr).GetByIdAsync(pendingManager.Id)).CanReview);
        Assert.Equal(AttendanceStatus.Valid,
            (await Service(s, Hr).ReviewAsync(pendingManager.Id, new ReviewAttendanceRequest { Approved = true })).Log.Status);
    }
}
