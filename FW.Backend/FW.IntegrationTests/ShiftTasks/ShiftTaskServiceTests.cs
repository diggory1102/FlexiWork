using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.ShiftTasks;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.ShiftTasks;

/// <summary>
/// Checklist công việc theo ca (Task 4.3) trên MySQL thật: quyền với mẫu checklist (Admin / Quản lý / HR), việc dự kiến trước khi vào ca,
/// giao việc theo chi nhánh & chức vụ, tích kèm ảnh / bỏ tích, bổ sung việc mới khi ca đang diễn ra, tiến độ.
/// Đồng hồ cố định 10/02/2035. Seed: người dùng 1 = Admin, 2 = Quản lý chi nhánh 1, 4 = HR; chức vụ 3, 4.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftTaskServiceTests
{
    private const int Branch1 = 1, Branch2 = 2, AdminId = 1, ManagerId = 2, HrId = 4, Cashier = 4, StoreManager = 3;
    private static readonly DateTime Now = new(2035, 2, 10, 10, 0, 0);

    private readonly DatabaseFixture _db;

    public ShiftTaskServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime Now => ShiftTaskServiceTests.Now;
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
        public List<string> Deleted { get; } = new();

        public Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            var id = $"{folder}/{Guid.NewGuid():N}";
            return Task.FromResult(new FileUploadResult($"https://img.test/{id}.jpg", id));
        }

        public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(publicId);
            return Task.FromResult(true);
        }
    }

    private static readonly TestUser Admin = new(AdminId, Branch1, RoleNames.Admin);
    private static readonly TestUser Manager1 = new(ManagerId, Branch1, RoleNames.Manager);
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);

    private static ShiftTaskService Service(DbSession s, ICurrentUserService user, MemoryStorage? storage = null) => new(
        new ShiftTaskRepository(s), new ShiftTaskAssignmentRepository(s), new ShiftRepository(s), new ScheduleRepository(s),
        new UserRepository(s), new PositionRepository(s), new BranchRepository(s), storage ?? new MemoryStorage(), user, new FixedClock(),
        NullLogger<ShiftTaskService>.Instance);

    private static ShiftTaskService As(DbSession s, User user, MemoryStorage? storage = null) => Service(s, new TestUser(user.Id, user.BranchId), storage);

    private static async Task<Shift> NewShiftAsync(DbSession s, int? branchId, int startHour = 8, int endHour = 17)
    {
        var shift = new Shift
        {
            ShiftCode = $"TK{Guid.NewGuid():N}"[..14], Name = $"Ca checklist {startHour}h", BranchId = branchId,
            StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour)
        };
        await new ShiftRepository(s).AddAsync(shift);
        return shift;
    }

    private static async Task<User> NewEmployeeAsync(DbSession s, int branchId = Branch1, int? positionId = Cashier)
    {
        var code = $"K{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = $"Nhân viên {code}", BranchId = branchId, PositionId = positionId
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static async Task<Schedule> NewScheduleAsync(DbSession s, User user, Shift shift, string status = ScheduleStatus.Scheduled,
        int branchId = Branch1)
    {
        var schedule = new Schedule
        {
            UserId = user.Id, ShiftId = shift.Id, BranchId = branchId, WorkDate = Now.Date, IsPublished = true,
            PublishedAt = Now.AddDays(-3), Status = status
        };
        await new ScheduleRepository(s).AddAsync(schedule);
        return schedule;
    }

    private static ShiftTaskTemplateRequest Template(Shift shift, string title, int? branchId = null, int? positionId = null,
        string phase = TaskPhase.During, bool photo = false, bool mandatory = true, int sort = 0) => new()
    {
        ShiftId = shift.Id, BranchId = branchId, PositionId = positionId, Title = title, Phase = phase, RequiresPhoto = photo,
        IsMandatory = mandatory, SortOrder = sort
    };

    /// <summary>Giả lập check-in: ca chuyển sang đang làm và giao checklist (như AttendanceService).</summary>
    private static async Task CheckInAsync(DbSession s, Schedule schedule, User user)
    {
        schedule.Status = ScheduleStatus.InProgress;
        await new ScheduleRepository(s).UpdateAsync(schedule);
        await new ShiftTaskAssignmentRepository(s).AssignForScheduleAsync(schedule.Id, user.Id, schedule.ShiftId, schedule.BranchId, user.PositionId);
    }

    private static TaskPhoto Photo() => new(new MemoryStream(new byte[] { 1, 2, 3 }), "quay.jpg", "image/jpeg", 3);

    // ------------------------------------------------------------------ Mẫu checklist

    [SkippableFact]
    public async Task Templates_follow_role_and_branch_permissions()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shared = await NewShiftAsync(s, null);
        var branch2Shift = await NewShiftAsync(s, Branch2);

        var common = await Service(s, Admin).CreateTemplateAsync(Template(shared, "Kiểm tra PCCC", phase: TaskPhase.End));
        Assert.Null(common.BranchId);
        var own = await Service(s, Manager1).CreateTemplateAsync(Template(shared, "Lau kính mặt tiền"));
        Assert.Equal(Branch1, own.BranchId); // Quản lý luôn thêm việc riêng của chi nhánh mình
        Assert.Equal(Branch2, (await Service(s, Admin).CreateTemplateAsync(Template(branch2Shift, "Đếm kho"))).BranchId); // ca riêng CN2

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, Manager1).CreateTemplateAsync(Template(shared, "x", branchId: Branch2)));
        await Assert.ThrowsAsync<BadRequestException>(() => Service(s, Manager1).CreateTemplateAsync(Template(branch2Shift, "x")));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, Hr).CreateTemplateAsync(Template(shared, "x")));
        await Assert.ThrowsAsync<BadRequestException>(() => Service(s, Admin).CreateTemplateAsync(Template(shared, "x", phase: "night")));
        await Assert.ThrowsAsync<BadRequestException>(() => Service(s, Admin).CreateTemplateAsync(Template(shared, "x", positionId: 99999)));

        // Quản lý không sửa việc chung của Admin; sửa được việc của chi nhánh mình
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, Manager1).UpdateTemplateAsync(common.Id, Template(shared, "Đổi tên")));
        var renamed = await Service(s, Manager1).UpdateTemplateAsync(own.Id, Template(shared, "Lau kính & cửa", photo: true, phase: TaskPhase.Start));
        Assert.Equal(("Lau kính & cửa", TaskPhase.Start, true), (renamed.Title, renamed.Phase, renamed.RequiresPhoto));

        var managerView = await Service(s, Manager1).GetTemplatesAsync(new ShiftTaskTemplateFilter { ShiftId = shared.Id });
        Assert.Equal(new[] { own.Id, common.Id }, managerView.Select(t => t.Id)); // đầu ca trước cuối ca
        Assert.Equal(new[] { true, false }, managerView.Select(t => t.CanEdit));
        var branch2View = await Service(s, new TestUser(ManagerId, Branch2, RoleNames.Manager))
            .GetTemplatesAsync(new ShiftTaskTemplateFilter { ShiftId = shared.Id });
        Assert.Equal(common.Id, Assert.Single(branch2View).Id); // không thấy việc riêng của chi nhánh 1
        Assert.Equal(2, (await Service(s, Hr).GetTemplatesAsync(new ShiftTaskTemplateFilter { ShiftId = shared.Id })).Count);
        Assert.All(await Service(s, Hr).GetTemplatesAsync(new ShiftTaskTemplateFilter { ShiftId = shared.Id }), t => Assert.False(t.CanEdit));

        await Service(s, Manager1).DeleteTemplateAsync(own.Id);
        Assert.Single(await Service(s, Hr).GetTemplatesAsync(new ShiftTaskTemplateFilter { ShiftId = shared.Id }));
    }

    // ------------------------------------------------------------------ Checklist trong ca

    [SkippableFact]
    public async Task Employee_sees_preview_then_completes_assigned_tasks_with_photos()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, null);
        var admin = Service(s, Admin);
        await admin.CreateTemplateAsync(Template(shift, "Bàn giao két", phase: TaskPhase.End, photo: true));
        await admin.CreateTemplateAsync(Template(shift, "Mở cửa", phase: TaskPhase.Start, sort: 1));
        await admin.CreateTemplateAsync(Template(shift, "Họp giao ca quản lý", positionId: StoreManager));          // chức vụ khác
        await admin.CreateTemplateAsync(Template(shift, "Việc chi nhánh 2", branchId: Branch2));                 // chi nhánh khác
        await admin.CreateTemplateAsync(Template(shift, "Sắp xếp kệ", mandatory: false));

        var employee = await NewEmployeeAsync(s);
        var schedule = await NewScheduleAsync(s, employee, shift);
        var storage = new MemoryStorage();
        var me = As(s, employee, storage);

        // Chưa check-in: thấy việc dự kiến (đầu ca → trong ca → cuối ca), chưa tích được
        var preview = Assert.Single(await me.GetMyTasksAsync(null));
        Assert.False(preview.IsAssigned);
        Assert.False(preview.CanUpdate);
        Assert.Equal(new[] { "Mở cửa", "Sắp xếp kệ", "Bàn giao két" }, preview.Tasks.Select(t => t.Title));
        Assert.All(preview.Tasks, t => Assert.Null(t.AssignmentId));
        Assert.Null(preview.Percent);

        await CheckInAsync(s, schedule, employee);
        var list = Assert.Single(await me.GetMyTasksAsync(null));
        Assert.True(list.IsAssigned && list.CanUpdate);
        Assert.Equal((3, 0, 0), (list.Total, list.Done, list.Percent));
        var open = list.Tasks.Single(t => t.Title == "Mở cửa").AssignmentId!.Value;
        var handover = list.Tasks.Single(t => t.Title == "Bàn giao két").AssignmentId!.Value;

        var afterOpen = await me.CompleteAsync(open, new CompleteTaskForm { Note = "Đã mở lúc 07:55" }, null);
        Assert.Equal((1, 33), (afterOpen.Done, afterOpen.Percent));
        Assert.Equal("Đã mở lúc 07:55", afterOpen.Tasks.Single(t => t.AssignmentId == open).Note);
        await Assert.ThrowsAsync<BadRequestException>(() => me.CompleteAsync(open, new CompleteTaskForm(), null)); // đã xong
        await Assert.ThrowsAsync<BadRequestException>(() => me.CompleteAsync(handover, new CompleteTaskForm(), null)); // thiếu ảnh
        var withPhoto = await me.CompleteAsync(handover, new CompleteTaskForm(), Photo());
        var photoUrl = withPhoto.Tasks.Single(t => t.AssignmentId == handover).ProofImageUrl;
        Assert.StartsWith("https://img.test/tasks/", photoUrl);

        // Người khác không tích hộ; bỏ tích thì ảnh bị xóa
        var other = await NewEmployeeAsync(s);
        await Assert.ThrowsAsync<ForbiddenException>(() => As(s, other).UncompleteAsync(handover));
        var undone = await me.UncompleteAsync(handover);
        Assert.Null(undone.Tasks.Single(t => t.AssignmentId == handover).ProofImageUrl);
        Assert.Single(storage.Deleted);

        // Việc mới thêm vào mẫu khi ca đang diễn ra được bổ sung khi mở checklist
        await admin.CreateTemplateAsync(Template(shift, "Kiểm tra hàng hết hạn"));
        Assert.Equal(4, Assert.Single(await me.GetMyTasksAsync(null)).Total);

        // Quản lý chi nhánh xem được, chi nhánh khác thì không; xong ca thì khóa checklist
        Assert.Equal(1, (await Service(s, Manager1).GetScheduleTasksAsync(schedule.Id)).Done);
        Assert.False((await Service(s, Manager1).GetScheduleTasksAsync(schedule.Id)).CanUpdate);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service(s, new TestUser(ManagerId, Branch2, RoleNames.Manager)).GetScheduleTasksAsync(schedule.Id));
        schedule.Status = ScheduleStatus.Completed;
        await new ScheduleRepository(s).UpdateAsync(schedule);
        var locked = await Assert.ThrowsAsync<BadRequestException>(() => me.CompleteAsync(handover, new CompleteTaskForm(), Photo()));
        Assert.Contains("đã check-out", locked.Message);
    }

    // ------------------------------------------------------------------ Tiến độ

    [SkippableFact]
    public async Task Progress_summarises_each_shift_of_the_day()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, Branch1, 13, 17);
        await Service(s, Manager1).CreateTemplateAsync(Template(shift, "Bắt buộc 1"));
        await Service(s, Manager1).CreateTemplateAsync(Template(shift, "Bắt buộc 2"));
        await Service(s, Manager1).CreateTemplateAsync(Template(shift, "Tùy chọn", mandatory: false));

        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s);
        var aSchedule = await NewScheduleAsync(s, a, shift);
        var bSchedule = await NewScheduleAsync(s, b, shift);
        await CheckInAsync(s, aSchedule, a);
        var aTasks = Assert.Single(await As(s, a).GetMyTasksAsync(null));
        await As(s, a).CompleteAsync(aTasks.Tasks.Single(t => t.Title == "Bắt buộc 1").AssignmentId!.Value, new CompleteTaskForm(), null);
        await As(s, a).CompleteAsync(aTasks.Tasks.Single(t => t.Title == "Tùy chọn").AssignmentId!.Value, new CompleteTaskForm(), null);

        var progress = await Service(s, Manager1).GetProgressAsync(new TaskProgressFilter());
        Assert.Equal(Branch1, progress.BranchId);
        var aRow = progress.Schedules.Single(r => r.ScheduleId == aSchedule.Id);
        Assert.Equal((3, 2, 2, 1, 67), (aRow.Total, aRow.Done, aRow.MandatoryTotal, aRow.MandatoryDone, aRow.Percent));
        var bRow = progress.Schedules.Single(r => r.ScheduleId == bSchedule.Id);
        Assert.Equal((0, ScheduleStatus.Scheduled), (bRow.Total, bRow.ScheduleStatus));
        Assert.Null(bRow.Percent);
        Assert.Equal(progress.Schedules.Sum(r => r.Total), progress.TotalTasks);
        Assert.True(progress.NotStarted >= 1);
        Assert.True(progress.MandatoryPending >= 1);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(s, Manager1).GetProgressAsync(new TaskProgressFilter { BranchId = Branch2 }));
        var hrView = await Service(s, Hr).GetProgressAsync(new TaskProgressFilter { BranchId = Branch1 });
        Assert.Contains(hrView.Schedules, r => r.ScheduleId == aSchedule.Id);
    }

    // ------------------------------------------------------------------ giao việc cho người đang trong ca

    [SkippableFact]
    public async Task A_new_template_goes_to_everyone_already_in_the_shift_and_is_expected_by_those_not_yet_checked_in()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, Branch1, 13, 17);
        var cashier1 = await NewEmployeeAsync(s);
        var cashier2 = await NewEmployeeAsync(s);
        var waiting = await NewEmployeeAsync(s);                              // chưa check-in
        var storeManager = await NewEmployeeAsync(s, positionId: StoreManager); // chức vụ khác
        var schedules = new Dictionary<int, Schedule>();
        foreach (var u in new[] { cashier1, cashier2, waiting, storeManager }) schedules[u.Id] = await NewScheduleAsync(s, u, shift);
        foreach (var u in new[] { cashier1, cashier2, storeManager }) await CheckInAsync(s, schedules[u.Id], u);

        // Thêm việc cho ca chiều, chỉ chức vụ Thu ngân — nobody opens any checklist afterwards
        await Service(s, Manager1).CreateTemplateAsync(Template(shift, "Việc mới của thu ngân", branchId: Branch1, positionId: Cashier));

        var progress = await Service(s, Manager1).GetProgressAsync(new TaskProgressFilter());
        int Total(User u) => progress.Schedules.Single(r => r.ScheduleId == schedules[u.Id].Id).Total;
        Assert.Equal(1, Total(cashier1)); // cả hai thu ngân đang trong ca đều có việc ngay
        Assert.Equal(1, Total(cashier2));
        Assert.Equal(0, Total(storeManager)); // chức vụ khác không nhận

        var wait = progress.Schedules.Single(r => r.ScheduleId == schedules[waiting.Id].Id);
        Assert.Equal((0, 1, 1), (wait.Total, wait.Expected, wait.ExpectedMandatory)); // chưa vào ca: dự kiến 1 việc, chưa giao
        Assert.Equal(0, progress.Schedules.Single(r => r.ScheduleId == schedules[storeManager.Id].Id).Expected);

        // Vào ca rồi thì nhận đúng việc đó, không bị giao trùng
        await CheckInAsync(s, schedules[waiting.Id], waiting);
        Assert.Equal(1, Assert.Single(await As(s, waiting).GetMyTasksAsync(null)).Tasks.Count);
    }

    [SkippableFact]
    public async Task Progress_repairs_missing_tasks_of_people_already_in_the_shift()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, Branch1, 13, 17);
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s);
        var aSchedule = await NewScheduleAsync(s, a, shift);
        var bSchedule = await NewScheduleAsync(s, b, shift);
        await CheckInAsync(s, aSchedule, a);
        await CheckInAsync(s, bSchedule, b);

        // Việc mẫu thêm trực tiếp vào CSDL (như dữ liệu cũ, trước khi có giao ngay): chưa ai có việc này
        await new ShiftTaskRepository(s).AddAsync(new ShiftTask { ShiftId = shift.Id, BranchId = Branch1, Title = "Việc thêm từ trước", Phase = TaskPhase.During, IsMandatory = true });

        var first = await Service(s, Manager1).GetProgressAsync(new TaskProgressFilter());
        Assert.Equal(1, first.Schedules.Single(r => r.ScheduleId == aSchedule.Id).Total);
        Assert.Equal(1, first.Schedules.Single(r => r.ScheduleId == bSchedule.Id).Total);

        // Xem lại (bấm "Làm mới") không giao trùng
        var second = await Service(s, Manager1).GetProgressAsync(new TaskProgressFilter());
        Assert.Equal(1, second.Schedules.Single(r => r.ScheduleId == aSchedule.Id).Total);
    }

    [SkippableFact]
    public async Task Widening_the_position_of_a_template_gives_it_to_the_people_now_covered()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var shift = await NewShiftAsync(s, Branch1, 13, 17);
        var cashier = await NewEmployeeAsync(s);
        var manager = await NewEmployeeAsync(s, positionId: StoreManager);
        var cashierSchedule = await NewScheduleAsync(s, cashier, shift);
        var managerSchedule = await NewScheduleAsync(s, manager, shift);
        await CheckInAsync(s, cashierSchedule, cashier);
        await CheckInAsync(s, managerSchedule, manager);

        var template = await Service(s, Manager1).CreateTemplateAsync(Template(shift, "Chỉ thu ngân", branchId: Branch1, positionId: Cashier));
        var before = await Service(s, Manager1).GetProgressAsync(new TaskProgressFilter());
        Assert.Equal(0, before.Schedules.Single(r => r.ScheduleId == managerSchedule.Id).Total);

        await Service(s, Manager1).UpdateTemplateAsync(template.Id, Template(shift, "Cho mọi chức vụ", branchId: Branch1, positionId: null));
        var after = await Service(s, Manager1).GetProgressAsync(new TaskProgressFilter());
        Assert.Equal(1, after.Schedules.Single(r => r.ScheduleId == managerSchedule.Id).Total);
        Assert.Equal(1, after.Schedules.Single(r => r.ScheduleId == cashierSchedule.Id).Total);
    }
}
