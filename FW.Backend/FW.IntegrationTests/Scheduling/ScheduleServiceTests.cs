using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Services;

namespace FW.IntegrationTests.Scheduling;

/// <summary>
/// Xếp lịch (Task 2.3) trên MySQL thật: nháp → công bố, chặn xung đột, sao chép tuần, xóa / hủy ca, phạm vi chi nhánh.
/// Dữ liệu seed: người dùng 2 (Quản lý chi nhánh 1), 3 (Nhân viên chi nhánh 1). Mỗi test dùng một khoảng ngày riêng
/// ở tương lai để không ảnh hưởng nhau.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ScheduleServiceTests
{
    private const int Branch1 = 1;
    private const int EmployeeId = 3;

    private readonly DatabaseFixture _db;

    public ScheduleServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int userId, int? branchId, params string[] roles)
        {
            UserId = userId;
            BranchId = branchId;
            Roles = roles;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles { get; }
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private static readonly TestUser Manager1 = new(2, Branch1, RoleNames.Manager);
    private static readonly TestUser Employee = new(EmployeeId, Branch1, RoleNames.Employee);
    private static readonly TestUser Hr = new(4, null, RoleNames.Hr);

    private static ScheduleService Service(DbSession s, ICurrentUserService user) => new(
        new ScheduleRepository(s), new ShiftRepository(s), new BranchRepository(s), new UserRepository(s), s, user, new DateTimeProvider());

    private static DateOnly Day(int offset) => DateOnly.FromDateTime(DateTime.Today).AddDays(offset);

    private static async Task<Shift> NewShiftAsync(DbSession s, int start, int end, int? branchId = Branch1)
    {
        var shift = new Shift
        {
            ShiftCode = $"X{Guid.NewGuid():N}"[..12],
            Name = $"Ca {start}-{end}",
            BranchId = branchId,
            StartTime = TimeSpan.FromHours(start),
            EndTime = TimeSpan.FromHours(end),
            IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(start), TimeSpan.FromHours(end))
        };
        await new ShiftRepository(s).AddAsync(shift);
        return shift;
    }

    private static CreateSchedulesRequest Items(params (int UserId, Shift Shift, DateOnly Date)[] items) => new()
    {
        Items = items.Select(i => new ScheduleItemRequest { UserId = i.UserId, ShiftId = i.Shift.Id, WorkDate = i.Date }).ToList()
    };

    [SkippableFact]
    public async Task Drafts_are_hidden_from_employee_until_published()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Service(session, Manager1);
        var morning = await NewShiftAsync(session, 8, 12);
        var (from, to) = (Day(200), Day(206));

        var created = await manager.CreateAsync(Items((EmployeeId, morning, Day(200)), (EmployeeId, morning, Day(201)), (EmployeeId, morning, Day(202))));
        Assert.Equal(3, created.Created);

        var grid = await manager.GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = from, ToDate = to });
        Assert.Equal(3, grid.Count(s => s.ShiftId == morning.Id));
        Assert.All(grid, s => Assert.False(s.IsPublished));
        Assert.Equal("08:00", grid[0].StartTime);
        Assert.Equal(Day(200), grid[0].WorkDate);

        Assert.Empty(await Service(session, Employee).GetMySchedulesAsync(from, to));

        Assert.Equal(3, await manager.PublishAsync(new ScheduleRangeRequest { FromDate = from, ToDate = to }));
        var mine = await Service(session, Employee).GetMySchedulesAsync(from, to);
        Assert.Equal(3, mine.Count);
        Assert.All(mine, s => Assert.True(s.IsPublished));

        var saved = await new ScheduleRepository(session).GetByIdAsync(mine[0].Id);
        Assert.Equal(Manager1.UserId, saved!.CreatedBy);
    }

    [SkippableFact]
    public async Task Any_conflict_rejects_the_whole_batch_but_split_shifts_are_allowed()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Service(session, Manager1);
        var night = await NewShiftAsync(session, 22, 6);
        var early = await NewShiftAsync(session, 5, 12);
        var morning = await NewShiftAsync(session, 8, 12);
        var afternoon = await NewShiftAsync(session, 12, 17);

        await manager.CreateAsync(Items((EmployeeId, night, Day(220))));

        var error = await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(
            (EmployeeId, early, Day(221)),     // ca đêm hôm trước kéo tới 06:00
            (EmployeeId, morning, Day(222)),
            (EmployeeId, morning, Day(222))))); // trùng trong cùng lần xếp
        Assert.Equal(2, error.Errors!.Count);
        Assert.Contains(error.Errors, e => e.Contains("chồng giờ") && e.Contains(night.ShiftCode));
        Assert.Contains(error.Errors, e => e.Contains("bị trùng"));

        // Không ca nào được lưu (kể cả ca hợp lệ)
        var grid = await manager.GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = Day(221), ToDate = Day(222) });
        Assert.DoesNotContain(grid, s => s.UserId == EmployeeId);

        // Ca gãy: hai ca nối tiếp trong ngày
        Assert.Equal(2, (await manager.CreateAsync(Items((EmployeeId, morning, Day(223)), (EmployeeId, afternoon, Day(223))))).Created);
    }

    [SkippableFact]
    public async Task Invalid_people_shifts_dates_and_branches_are_rejected()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Service(session, Manager1);
        var morning = await NewShiftAsync(session, 8, 12);
        var otherBranchShift = await NewShiftAsync(session, 8, 12, branchId: 2);

        var code = $"B2{Guid.NewGuid():N}"[..12];
        var outsider = new User { EmployeeCode = code, Email = $"{code}@test.local", FullName = "NV chi nhánh 2", PasswordHash = "x", BranchId = 2 };
        await new UserRepository(session).AddAsync(outsider);

        async Task<string> Reject(CreateSchedulesRequest request) =>
            Assert.Single((await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(request))).Errors!);

        Assert.Contains("không thuộc chi nhánh", await Reject(Items((outsider.Id, morning, Day(240)))));
        Assert.Contains("không áp dụng cho chi nhánh", await Reject(Items((EmployeeId, otherBranchShift, Day(240)))));
        Assert.Contains("ngày đã qua", await Reject(Items((EmployeeId, morning, Day(-1)))));
        Assert.Contains("không tồn tại", await Reject(Items((999_999, morning, Day(240)))));

        // Quản lý không xếp được cho chi nhánh khác; HR phải chọn chi nhánh
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.CreateAsync(new CreateSchedulesRequest
        {
            BranchId = 2, Items = Items((outsider.Id, otherBranchShift, Day(240))).Items
        }));
        await Assert.ThrowsAsync<BadRequestException>(() => Service(session, Hr).GetBranchSchedulesAsync(
            new ScheduleRangeQuery { FromDate = Day(240), ToDate = Day(246) }));
        await Assert.ThrowsAsync<BadRequestException>(() => manager.GetBranchSchedulesAsync(
            new ScheduleRangeQuery { FromDate = Day(240), ToDate = Day(340) })); // quá 62 ngày
    }

    [SkippableFact]
    public async Task Copying_a_week_creates_drafts_and_skips_conflicts()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Service(session, Manager1);
        var morning = await NewShiftAsync(session, 8, 12);
        var office = await NewShiftAsync(session, 8, 17);

        await manager.CreateAsync(Items((EmployeeId, morning, Day(260)), (EmployeeId, morning, Day(261))));
        await manager.PublishAsync(new ScheduleRangeRequest { FromDate = Day(260), ToDate = Day(266) });

        // Tuần đích đã có ca hành chính ngày 268 → ca sáng sao chép sang ngày 268 bị bỏ qua
        await manager.CreateAsync(Items((EmployeeId, office, Day(268))));

        await Assert.ThrowsAsync<BadRequestException>(() => manager.CopyAsync(
            new CopySchedulesRequest { SourceFrom = Day(260), TargetFrom = Day(263), Days = 7 })); // hai khoảng chồng nhau

        var result = await manager.CopyAsync(new CopySchedulesRequest { SourceFrom = Day(260), TargetFrom = Day(267), Days = 7 });
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Contains("chồng giờ", Assert.Single(result.SkippedReasons));

        var copied = (await manager.GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = Day(267), ToDate = Day(267) }))
            .Single(s => s.UserId == EmployeeId);
        Assert.Equal(morning.Id, copied.ShiftId);
        Assert.False(copied.IsPublished);
    }

    [SkippableFact]
    public async Task Drafts_are_deleted_and_published_shifts_are_cancelled()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Service(session, Manager1);
        var morning = await NewShiftAsync(session, 8, 12);

        await manager.CreateAsync(Items((EmployeeId, morning, Day(280)), (EmployeeId, morning, Day(281)), (EmployeeId, morning, Day(282))));
        var drafts = await manager.GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = Day(280), ToDate = Day(282) });

        Assert.True(await manager.RemoveAsync(drafts[0].Id)); // nháp → xóa hẳn
        Assert.Null(await new ScheduleRepository(session).GetByIdAsync(drafts[0].Id));

        await manager.PublishAsync(new ScheduleRangeRequest { FromDate = Day(281), ToDate = Day(281) });
        Assert.False(await manager.RemoveAsync(drafts[1].Id)); // đã công bố → hủy
        var cancelled = (await Service(session, Employee).GetMySchedulesAsync(Day(281), Day(281))).Single();
        Assert.Equal(ScheduleStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<BadRequestException>(() => manager.RemoveAsync(drafts[1].Id)); // hủy lần 2

        // Ca đã hủy không còn chặn việc xếp ca khác vào cùng giờ
        var office = await NewShiftAsync(session, 8, 17);
        Assert.Equal(1, (await manager.CreateAsync(Items((EmployeeId, office, Day(281))))).Created);

        Assert.Equal(2, await manager.DeleteDraftsAsync(new ScheduleRangeRequest { FromDate = Day(280), ToDate = Day(282) }));
    }
}
