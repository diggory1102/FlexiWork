using FW.Application.Common.Interfaces;
using FW.Application.Features.Assistant;
using FW.Application.Features.Requests;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.Requests;

/// <summary>
/// Tìm đồng nghiệp ở chi nhánh đông hơn một trang dữ liệu (100 người): danh sách đổi ca và trợ lý AI phải thấy cả người
/// đứng cuối theo mã nhân viên. Mỗi lần chạy dùng một chi nhánh mới để không đụng dữ liệu khác.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ColleagueLookupTests
{
    private static readonly DateTime Now = new(2038, 3, 10, 10, 0, 0);
    private static readonly DateTime Day = new(2038, 3, 12);

    private readonly DatabaseFixture _db;

    public ColleagueLookupTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => ColleagueLookupTests.Now;
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int userId, int branchId)
        {
            UserId = userId;
            BranchId = branchId;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles => new[] { RoleNames.Employee };
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == RoleNames.Employee;
    }

    private static async Task<User> NewUserAsync(DbSession s, int branchId, string codePrefix, string fullName, string status = UserStatus.Active)
    {
        var code = $"{codePrefix}{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = fullName, BranchId = branchId, Status = status
        };
        var users = new UserRepository(s);
        await users.AddAsync(user);
        var role = (await new RoleRepository(s).GetByNameAsync(RoleNames.Employee))!;
        await users.SetRolesAsync(user.Id, new[] { role.Id });
        return user;
    }

    [SkippableFact]
    public async Task Colleagues_beyond_the_first_hundred_are_still_found()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var branchId = await new BranchRepository(s).AddAsync(new Branch
        {
            BranchCode = $"C{Guid.NewGuid():N}"[..10].ToUpperInvariant(), Name = "Chi nhánh đông người", Address = "1 Phố Thử",
            Latitude = 21.0m, Longitude = 105.8m, Radius = 50
        });
        var requester = await NewUserAsync(s, branchId, "AA", "Người Tạo Đơn");
        for (var i = 0; i < 100; i++) await NewUserAsync(s, branchId, "BB", $"Đồng Nghiệp {i:000}");
        // Mã bắt đầu bằng ZZ: đứng cuối khi sắp theo mã nhân viên, tức nằm ngoài trang 100 người đầu tiên
        var last = await NewUserAsync(s, branchId, "ZZ", "Đặng Thị Dũng");
        var suspended = await NewUserAsync(s, branchId, "ZZ", "Đặng Thị Dung Khóa", UserStatus.Suspended);
        var deleted = await NewUserAsync(s, branchId, "ZZ", "Đặng Thị Dung Xóa");
        await new UserRepository(s).DeleteAsync(deleted.Id);

        var shift = new Shift
        {
            ShiftCode = $"CL{Guid.NewGuid():N}"[..14], Name = "Ca sáng", BranchId = branchId,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12)
        };
        await new ShiftRepository(s).AddAsync(shift);
        var schedule = new Schedule
        {
            UserId = requester.Id, ShiftId = shift.Id, BranchId = branchId, WorkDate = Day, IsPublished = true, PublishedAt = Now.AddDays(-5)
        };
        await new ScheduleRepository(s).AddAsync(schedule);

        // Màn hình đổi ca: đủ mọi đồng nghiệp đang làm, không có chính mình, người bị khóa hay đã xóa
        var checker = new RequestScheduleChecker(new ScheduleRepository(s), new ShiftRepository(s), new UserRepository(s),
            new LeaveRequestRepository(s), new FixedClock());
        var options = await checker.GetSwapOptionsAsync(requester.Id, schedule.Id);
        Assert.Equal(101, options.Colleagues.Count);
        Assert.Contains(options.Colleagues, c => c.Id == last.Id);
        Assert.DoesNotContain(options.Colleagues, c => c.Id == requester.Id || c.Id == suspended.Id || c.Id == deleted.Id);

        // Trợ lý AI: gõ không dấu vẫn ra đúng người. Tìm đồng nghiệp không dùng tới service đơn từ / bảng công.
        var hr = new HrDataService(new LeaveTypeRepository(s), new ScheduleRepository(s), new AttendanceLogRepository(s),
            new UserRepository(s), requests: null!, timesheets: null!, new TestUser(requester.Id, branchId));
        var found = Assert.Single(await hr.FindColleaguesAsync(requester.Id, "dang thi dung"));
        Assert.Equal((last.Id, last.EmployeeCode), (found.UserId, found.EmployeeCode));
    }
}
