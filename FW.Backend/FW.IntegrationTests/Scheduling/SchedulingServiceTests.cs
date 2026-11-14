using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Services;

namespace FW.IntegrationTests.Scheduling;

/// <summary>
/// Nghiệp vụ khung ca & quy tắc vi phạm (Task 2.2) trên MySQL thật: phân quyền theo chi nhánh,
/// kiểm tra giờ giấc, khóa sửa giờ khi ca đã được xếp lịch, lưu quy tắc theo bậc.
/// Dữ liệu seed: chi nhánh 1, 2; người dùng 3 (nhân viên chi nhánh 1); ca dùng chung CA-SANG (id 1).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SchedulingServiceTests
{
    private readonly DatabaseFixture _db;

    public SchedulingServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int? branchId, params string[] roles)
        {
            BranchId = branchId;
            Roles = roles;
        }

        public int? UserId => 1;
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles { get; }
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private static readonly TestUser Hr = new(null, RoleNames.Hr);
    private static readonly TestUser Manager1 = new(1, RoleNames.Manager);

    private static ShiftService Shifts(DbSession s, ICurrentUserService user) =>
        new(new ShiftRepository(s), new BranchRepository(s), user, new DateTimeProvider());

    private static string Code() => $"T{Guid.NewGuid():N}"[..12];

    private static ShiftRequest NewShift(string start = "08:00", string end = "17:00", int? branchId = null) => new()
    {
        ShiftCode = Code(),
        Name = "Ca kiểm thử",
        BranchId = branchId,
        StartTime = start,
        EndTime = end,
        BreakMinutes = 60,
        WorkUnits = 1,
        LateGraceMinutes = 5,
        EarlyLeaveGraceMinutes = 5
    };

    [SkippableFact]
    public async Task Hr_creates_shared_and_cross_day_shifts_with_validation()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Shifts(session, Hr);

        var request = NewShift("22:00", "6:00");
        request.ShiftCode = $"  {request.ShiftCode.ToLowerInvariant()} ";
        var night = await service.CreateAsync(request);

        Assert.Equal(request.ShiftCode.Trim().ToUpperInvariant(), night.ShiftCode);
        Assert.Null(night.BranchId);
        Assert.True(night.IsCrossDay);
        Assert.Equal("22:00", night.StartTime);
        Assert.Equal("06:00", night.EndTime);
        Assert.Equal(420, night.WorkingMinutes);
        Assert.True(night.CanEdit);

        // Trùng mã (không phân biệt hoa thường)
        var duplicate = NewShift();
        duplicate.ShiftCode = night.ShiftCode.ToLowerInvariant();
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(duplicate));

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewShift("8h", "17:00")));   // sai định dạng
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewShift("25:00", "17:00")));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewShift("08:00", "08:00"))); // trùng giờ
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewShift("06:00", "23:00"))); // quá 16 giờ
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewShift("08:00", "09:00"))); // nghỉ 60' ≥ độ dài ca
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewShift(branchId: 999_999))); // chi nhánh không tồn tại
    }

    [SkippableFact]
    public async Task Manager_only_manages_shifts_of_own_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var manager = Shifts(session, Manager1);
        var hr = Shifts(session, Hr);

        // Bỏ trống chi nhánh → tự gán chi nhánh của Quản lý
        var own = await manager.CreateAsync(NewShift("13:00", "17:00"));
        Assert.Equal(1, own.BranchId);
        Assert.True(own.CanEdit);

        await Assert.ThrowsAsync<ForbiddenException>(() => manager.CreateAsync(NewShift(branchId: 2)));

        var otherBranch = await hr.CreateAsync(NewShift("13:00", "17:00", branchId: 2));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetByIdAsync(otherBranch.Id));

        // Ca dùng chung: xem được, không sửa / xóa được
        var shared = await manager.GetByIdAsync(1);
        Assert.False(shared.Shift.CanEdit);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.UpdateAsync(1, NewShift()));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.DeleteAsync(1));

        // Danh sách của Quản lý: ca dùng chung + ca chi nhánh mình, không có ca chi nhánh 2 (kể cả khi cố lọc chi nhánh 2)
        var list = await manager.GetPagedAsync(new ShiftFilter { BranchId = 2, PageSize = 100 });
        Assert.Contains(list.Items, s => s.Id == own.Id);
        Assert.Contains(list.Items, s => s.BranchId == null);
        Assert.DoesNotContain(list.Items, s => s.Id == otherBranch.Id);

        var available = await manager.GetAvailableForBranchAsync(1);
        Assert.Contains(available, s => s.Id == own.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetAvailableForBranchAsync(2));
    }

    [SkippableFact]
    public async Task Scheduled_shift_keeps_its_hours_and_cannot_be_deleted_while_upcoming()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Shifts(session, Hr);
        var schedules = new ScheduleRepository(session);

        var request = NewShift(branchId: 1);
        var shift = await service.CreateAsync(request);
        Assert.False((await service.GetByIdAsync(shift.Id)).IsInUse);

        var scheduleId = await schedules.AddAsync(new Schedule
        {
            UserId = 3, ShiftId = shift.Id, BranchId = 1, WorkDate = DateTime.Today.AddDays(1)
        });
        Assert.True((await service.GetByIdAsync(shift.Id)).IsInUse);

        // Đổi giờ → bị chặn; chỉ đổi tên và số phút cho phép → được
        var changeHours = NewShift("09:00", "18:00", branchId: 1);
        changeHours.ShiftCode = shift.ShiftCode;
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(shift.Id, changeHours));

        request.ShiftCode = shift.ShiftCode;
        request.Name = "Ca hành chính (đổi tên)";
        request.LateGraceMinutes = 10;
        var renamed = await service.UpdateAsync(shift.Id, request);
        Assert.Equal("Ca hành chính (đổi tên)", renamed.Name);
        Assert.Equal(10, renamed.LateGraceMinutes);

        // Còn lịch sắp tới → không xóa được; hủy lịch rồi mới xóa được
        await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(shift.Id));
        var schedule = await schedules.GetByIdAsync(scheduleId);
        schedule!.Status = ScheduleStatus.Cancelled;
        await schedules.UpdateAsync(schedule);
        await service.DeleteAsync(shift.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(shift.Id));
    }

    [SkippableFact]
    public async Task Attendance_policy_is_seeded_validated_and_replaced_atomically()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = new AttendancePolicyService(new ViolationRuleRepository(session), new SystemSettingRepository(session), session);

        var seeded = await service.GetAsync();
        Assert.Equal(26, seeded.StandardWorkDays);
        Assert.Equal(6, seeded.Rules.Count);
        Assert.Equal(new[] { 1, 15, 60 }, seeded.Rules.Where(r => r.ViolationType == ViolationType.Late).Select(r => r.MinMinutes));

        ViolationRuleRequest Tier(string type, int min, decimal deduct, decimal penalty) =>
            new() { ViolationType = type, MinMinutes = min, DeductWorkUnits = deduct, PenaltyAmount = penalty };

        // Không hợp lệ: trùng mốc, bậc sau nhẹ hơn bậc trước, bậc không phạt gì, loại sai → không thay đổi dữ liệu
        var invalid = new AttendancePolicyRequest
        {
            StandardWorkDays = 24,
            Rules =
            {
                Tier("late", 10, 0, 30_000), Tier("late", 10, 0, 40_000),
                Tier("late", 30, 0, 20_000),
                Tier("early_leave", 5, 0, 0),
                Tier("absent", 1, 1, 0)
            }
        };
        var error = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(invalid));
        Assert.True(error.Errors!.Count >= 4);
        Assert.Equal(26, (await service.GetAsync()).StandardWorkDays);

        var updated = await service.UpdateAsync(new AttendancePolicyRequest
        {
            StandardWorkDays = 24.5m,
            Rules = { Tier(" LATE ", 30, 0.5m, 100_000), Tier("late", 10, 0, 30_000), Tier("early_leave", 1, 0, 10_000) }
        });
        Assert.Equal(24.5m, updated.StandardWorkDays);
        Assert.Equal(3, updated.Rules.Count);
        Assert.Equal(new[] { 10, 30 }, updated.Rules.Where(r => r.ViolationType == ViolationType.Late).Select(r => r.MinMinutes));
        Assert.Equal(3, (await service.GetRulesAsync()).Count);

        // Danh sách rỗng = bỏ toàn bộ quy tắc phạt
        var cleared = await service.UpdateAsync(new AttendancePolicyRequest { StandardWorkDays = 26 });
        Assert.Empty(cleared.Rules);
    }
}
