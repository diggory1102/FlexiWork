using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Services;
using FW.IntegrationTests.Notifications;

namespace FW.IntegrationTests.Scheduling;

/// <summary>
/// Ngoại lệ định biên ca (Task 6.6, bước 2) trên MySQL thật: đặt / thay / bỏ ngoại lệ theo ngày và theo thứ, quyền theo chi nhánh,
/// định biên hiệu lực, và việc xếp lịch dùng đúng mức của từng ngày (không đụng người đã xếp, không ảnh hưởng ngày khác / chi nhánh khác).
/// Mỗi test tạo ca và nhân viên riêng nên không ảnh hưởng nhau.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftStaffingRuleTests
{
    private const int Branch1 = 1;

    private readonly DatabaseFixture _db;

    public ShiftStaffingRuleTests(DatabaseFixture db)
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
    private static readonly TestUser Hr = new(4, null, RoleNames.Hr);

    private static ShiftStaffingService Staffing(DbSession s, ICurrentUserService user) =>
        new(new ShiftStaffingRuleRepository(s), new ShiftRepository(s), new BranchRepository(s), user, new DateTimeProvider());

    private static ScheduleService Schedules(DbSession s, ICurrentUserService user) => new(
        new ScheduleRepository(s), new ShiftRepository(s), new ShiftStaffingRuleRepository(s), new BranchRepository(s), new UserRepository(s),
        new LeaveRequestRepository(s), new RecordingNotifier(), s, user, new DateTimeProvider());

    private static DateOnly Day(int offset) => DateOnly.FromDateTime(DateTime.Today).AddDays(offset);

    private static async Task<Shift> NewShiftAsync(DbSession s, int? branchId = Branch1, int? maxStaff = null, int? minStaff = null)
    {
        var shift = new Shift
        {
            ShiftCode = $"R{Guid.NewGuid():N}"[..12],
            Name = "Ca ngoại lệ",
            BranchId = branchId,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(12),
            MaxStaff = maxStaff,
            MinStaff = minStaff
        };
        await new ShiftRepository(s).AddAsync(shift);
        return shift;
    }

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId = Branch1)
    {
        var code = $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User { EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.local", FullName = $"NV {code}", PasswordHash = "x", BranchId = branchId };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static StaffingRuleRequest DateRule(Shift shift, DateOnly date, int? min, int? max, string? note = null, int? branchId = null) => new()
    {
        BranchId = branchId, ShiftId = shift.Id, Scope = StaffingScopes.Date, Date = date, MinStaff = min, MaxStaff = max, Note = note
    };

    private static StaffingRuleRequest WeekdayRule(Shift shift, int isoDay, int? min, int? max) => new()
    {
        ShiftId = shift.Id, Scope = StaffingScopes.Weekday, DayOfWeek = isoDay, MinStaff = min, MaxStaff = max
    };

    private static CreateSchedulesRequest Items(Shift shift, DateOnly day, params User[] people) => new()
    {
        Items = people.Select(p => new ScheduleItemRequest { UserId = p.Id, ShiftId = shift.Id, WorkDate = day }).ToList()
    };

    private static int IsoDay(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    // ------------------------------------------------------------------ đặt / thay / bỏ ngoại lệ

    [SkippableFact]
    public async Task Rules_are_saved_replaced_listed_and_removed()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Staffing(session, Manager1);
        var shift = await NewShiftAsync(session, maxStaff: 3, minStaff: 2);

        var saved = await service.SaveAsync(DateRule(shift, Day(40), 5, 6, "  Tiệc công ty ABC  "));
        Assert.Equal((StaffingScopes.Date, Day(40), (int?)null), (saved.Scope, saved.Date, saved.DayOfWeek));
        Assert.Equal((5, 6, "Tiệc công ty ABC"), (saved.MinStaff, saved.MaxStaff, saved.Note));
        Assert.Equal(shift.ShiftCode, saved.ShiftCode);

        // Đặt lại cùng ô → thay bản cũ, không sinh dòng thứ hai
        var replaced = await service.SaveAsync(DateRule(shift, Day(40), null, 8, "Đông hơn dự kiến"));
        Assert.Equal(saved.Id, replaced.Id);
        Assert.Equal((null, 8, "Đông hơn dự kiến"), (replaced.MinStaff, replaced.MaxStaff, replaced.Note));

        var weekday = await service.SaveAsync(WeekdayRule(shift, 6, 4, 4));
        Assert.Equal((StaffingScopes.Weekday, 6, (DateOnly?)null), (weekday.Scope, weekday.DayOfWeek, weekday.Date));

        var listed = (await service.GetRulesAsync(null)).Where(r => r.ShiftId == shift.Id).ToList();
        Assert.Equal(2, listed.Count);

        await service.DeleteAsync(saved.Id);
        Assert.Single((await service.GetRulesAsync(null)).Where(r => r.ShiftId == shift.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(saved.Id));
    }

    [SkippableFact]
    public async Task Invalid_scopes_values_shifts_and_branches_are_rejected()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Staffing(session, Manager1);
        var shift = await NewShiftAsync(session);
        var otherBranchShift = await NewShiftAsync(session, branchId: 2);

        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(DateRule(shift, Day(-1), 1, 2)));   // ngày đã qua
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(DateRule(shift, Day(400), 1, 2)));  // quá xa
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(DateRule(shift, Day(10), 5, 2)));   // tối thiểu > tối đa
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(new StaffingRuleRequest { ShiftId = shift.Id, Scope = "date" })); // thiếu ngày
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(new StaffingRuleRequest { ShiftId = shift.Id, Scope = "weekday" })); // thiếu thứ
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(new StaffingRuleRequest { ShiftId = shift.Id, Scope = "month" }));
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(DateRule(otherBranchShift, Day(10), 1, 2))); // ca của chi nhánh khác
        await Assert.ThrowsAsync<BadRequestException>(() => manager.SaveAsync(DateRule(new Shift { Id = 999_999 }, Day(10), 1, 2)));

        // Quản lý không đặt cho chi nhánh khác; HR phải chọn chi nhánh
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.SaveAsync(DateRule(otherBranchShift, Day(10), 1, 2, branchId: 2)));
        await Assert.ThrowsAsync<BadRequestException>(() => Staffing(session, Hr).SaveAsync(DateRule(shift, Day(10), 1, 2)));
        Assert.Equal(1, (await Staffing(session, Hr).SaveAsync(DateRule(shift, Day(10), 1, 2, branchId: Branch1))).MinStaff);
    }

    [SkippableFact]
    public async Task A_manager_cannot_remove_a_rule_of_another_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var shared = await NewShiftAsync(session, branchId: null);
        var rule = await Staffing(session, Hr).SaveAsync(DateRule(shared, Day(20), 1, 2, branchId: 2));

        await Assert.ThrowsAsync<ForbiddenException>(() => Staffing(session, Manager1).DeleteAsync(rule.Id));
        Assert.Empty((await Staffing(session, Manager1).GetRulesAsync(null)).Where(r => r.Id == rule.Id)); // và không thấy ngoại lệ của chi nhánh 2
    }

    // ------------------------------------------------------------------ định biên hiệu lực

    [SkippableFact]
    public async Task Effective_staffing_applies_rules_over_the_default_and_skips_unlimited_days()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Staffing(session, Manager1);
        var shift = await NewShiftAsync(session, maxStaff: 3, minStaff: 2);
        var unlimited = await NewShiftAsync(session); // chỉ có ngoại lệ, không có mặc định
        var party = Day(60);
        var rangeFrom = party.AddDays(-2);

        await service.SaveAsync(DateRule(shift, party, 5, 6, "Tiệc"));
        await service.SaveAsync(WeekdayRule(unlimited, IsoDay(party.AddDays(1)), null, 2));

        var effective = await service.GetEffectiveAsync(null, rangeFrom, rangeFrom.AddDays(6));
        var mine = effective.Where(e => e.ShiftId == shift.Id).ToList();
        Assert.Equal(7, mine.Count); // ca có mặc định → ngày nào cũng có giới hạn
        var partyDay = mine.Single(e => e.Date == party);
        Assert.Equal((5, 6, "date", "Tiệc"), (partyDay.MinStaff, partyDay.MaxStaff, partyDay.Source, partyDay.Note));
        Assert.All(mine.Where(e => e.Date != party), e => Assert.Equal((2, 3, "default"), (e.MinStaff, e.MaxStaff, e.Source)));

        var onlyWeekday = effective.Where(e => e.ShiftId == unlimited.Id).ToList();
        Assert.Single(onlyWeekday); // 7 ngày nhưng chỉ ngày trùng thứ của ngoại lệ có giới hạn
        Assert.Equal((party.AddDays(1), 2, "weekday"), (onlyWeekday[0].Date, onlyWeekday[0].MaxStaff, onlyWeekday[0].Source));

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetEffectiveAsync(null, rangeFrom, rangeFrom.AddDays(100))); // quá 62 ngày
    }

    // ------------------------------------------------------------------ xếp lịch theo định biên hiệu lực

    [SkippableFact]
    public async Task A_date_rule_lets_more_people_in_on_that_day_only_and_never_touches_those_already_scheduled()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Schedules(session, Manager1);
        var shift = await NewShiftAsync(session, maxStaff: 1);
        var people = new List<User>();
        for (var i = 0; i < 5; i++) people.Add(await NewEmployeeAsync(session));
        var (normal, party) = (Day(80), Day(81));

        // Mặc định: 1 người / ngày
        await manager.CreateAsync(Items(shift, normal, people[0]));
        await manager.CreateAsync(Items(shift, party, people[0]));
        await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, party, people[1])));

        // Ngày tiệc: nới lên 3 → xếp thêm được; người đã xếp từ trước giữ nguyên
        await Staffing(session, Manager1).SaveAsync(DateRule(shift, party, 3, 3, "Tiệc đặt bàn"));
        Assert.Equal(2, (await manager.CreateAsync(Items(shift, party, people[1], people[2]))).Created);
        var rejected = await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, party, people[3])));
        Assert.Contains("(3/3)", Assert.Single(rejected.Errors!));

        // Ngày thường vẫn 1 người
        await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, normal, people[4])));
        var grid = await manager.GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = normal, ToDate = party });
        Assert.Equal(1, grid.Count(s => s.ShiftId == shift.Id && s.WorkDate == normal));
        Assert.Equal(3, grid.Count(s => s.ShiftId == shift.Id && s.WorkDate == party));
    }

    [SkippableFact]
    public async Task Lowering_a_rule_below_the_current_headcount_removes_nobody_and_blocks_new_people()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Schedules(session, Manager1);
        var shift = await NewShiftAsync(session, maxStaff: 3);
        var (a, b, c, d) = (await NewEmployeeAsync(session), await NewEmployeeAsync(session), await NewEmployeeAsync(session), await NewEmployeeAsync(session));
        var day = Day(90);

        await manager.CreateAsync(Items(shift, day, a, b, c));
        await Staffing(session, Manager1).SaveAsync(DateRule(shift, day, null, 1));

        var error = await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, day, d)));
        Assert.Contains("(3/1)", Assert.Single(error.Errors!)); // ô đang vượt, vẫn đủ 3 người
        var grid = await manager.GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = day, ToDate = day });
        Assert.Equal(3, grid.Count(s => s.ShiftId == shift.Id));
    }

    [SkippableFact]
    public async Task An_empty_rule_lifts_the_default_limit_and_removing_it_restores_the_default()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Schedules(session, Manager1);
        var shift = await NewShiftAsync(session, maxStaff: 1);
        var (a, b, c) = (await NewEmployeeAsync(session), await NewEmployeeAsync(session), await NewEmployeeAsync(session));
        var day = Day(100);

        var rule = await Staffing(session, Manager1).SaveAsync(DateRule(shift, day, null, null, "Không giới hạn hôm nay"));
        Assert.Equal(3, (await manager.CreateAsync(Items(shift, day, a, b, c))).Created);

        await Staffing(session, Manager1).DeleteAsync(rule.Id);
        var d = await NewEmployeeAsync(session);
        await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, day, d))); // quay về mặc định (1) → đã vượt
    }

    [SkippableFact]
    public async Task A_weekday_rule_applies_every_week_and_the_date_rule_still_wins()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Schedules(session, Manager1);
        var shift = await NewShiftAsync(session); // không có mặc định
        var people = new List<User>();
        for (var i = 0; i < 4; i++) people.Add(await NewEmployeeAsync(session));
        var saturday = Day(110);
        while (IsoDay(saturday) != 6) saturday = saturday.AddDays(1);
        var nextSaturday = saturday.AddDays(7);

        await Staffing(session, Manager1).SaveAsync(WeekdayRule(shift, 6, null, 1));          // mọi thứ Bảy tối đa 1 người
        await manager.CreateAsync(Items(shift, saturday, people[0]));
        await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, saturday, people[1])));
        await manager.CreateAsync(Items(shift, saturday.AddDays(1), people[1], people[2]));   // Chủ nhật: không giới hạn
        await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, nextSaturday, people[1], people[2]))); // thứ Bảy sau cũng bị chặn

        await Staffing(session, Manager1).SaveAsync(DateRule(shift, nextSaturday, null, 2)); // riêng thứ Bảy sau nới lên 2
        Assert.Equal(2, (await manager.CreateAsync(Items(shift, nextSaturday, people[1], people[2]))).Created);
        await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(Items(shift, nextSaturday, people[3])));
    }

    [SkippableFact]
    public async Task The_rule_of_one_branch_does_not_limit_the_same_shared_shift_in_another_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var hr = Schedules(session, Hr);
        var shared = await NewShiftAsync(session, branchId: null);
        var (inBranch1, inBranch2, second) = (await NewEmployeeAsync(session, Branch1), await NewEmployeeAsync(session, 2), await NewEmployeeAsync(session, 2));
        var day = Day(120);

        await Staffing(session, Hr).SaveAsync(DateRule(shared, day, null, 1, branchId: Branch1));
        Assert.Equal(1, (await hr.CreateAsync(new CreateSchedulesRequest { BranchId = Branch1, Items = Items(shared, day, inBranch1).Items })).Created);

        // Chi nhánh 2 không có ngoại lệ và ca không có mặc định → không giới hạn
        Assert.Equal(2, (await hr.CreateAsync(new CreateSchedulesRequest { BranchId = 2, Items = Items(shared, day, inBranch2, second).Items })).Created);
    }

    [SkippableFact]
    public async Task Two_managers_taking_the_last_place_allowed_by_a_date_rule_never_exceed_it()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var setup = _db.CreateSession();
        var shift = await NewShiftAsync(setup); // không có mặc định: chỉ ngoại lệ mới giới hạn → đường khóa phải bật theo ngoại lệ
        var rounds = new List<(DateOnly Day, User A, User B)>();
        for (var i = 0; i < 5; i++)
        {
            var day = Day(130 + i);
            await Staffing(setup, Manager1).SaveAsync(DateRule(shift, day, null, 1));
            rounds.Add((day, await NewEmployeeAsync(setup), await NewEmployeeAsync(setup)));
        }

        foreach (var (day, a, b) in rounds)
        {
            await using var sessionA = _db.CreateSession();
            await using var sessionB = _db.CreateSession();
            var results = await Task.WhenAll(Attempt(Schedules(sessionA, Manager1), Items(shift, day, a)), Attempt(Schedules(sessionB, Manager1), Items(shift, day, b)));

            Assert.Equal(1, results.Count(ok => ok));
            Assert.Equal(1, (await Schedules(setup, Manager1).GetBranchSchedulesAsync(new ScheduleRangeQuery { FromDate = day, ToDate = day })).Count(s => s.ShiftId == shift.Id));
        }

        static async Task<bool> Attempt(ScheduleService service, CreateSchedulesRequest request)
        {
            try
            {
                await service.CreateAsync(request);
                return true;
            }
            catch (BadRequestException)
            {
                return false;
            }
        }
    }
}
