using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Requests;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Requests;

/// <summary>
/// Duyệt đơn & quỹ phép (Task 4.2): trừ phép + ca chuyển sang nghỉ, đổi người làm ca, OT / giải trình, quyền duyệt, hoàn tác khi lỗi,
/// chặn xếp ca vào ngày đã duyệt nghỉ, HR điều chỉnh quỹ phép. Dùng chung dữ liệu mẫu với <see cref="LeaveRequestServiceTests"/>.
/// </summary>
public partial class LeaveRequestServiceTests
{
    private static readonly TestUser Manager1 = new(ManagerId, Branch1, RoleNames.Manager);
    private static readonly TestUser Hr = new(HrId, null, RoleNames.Hr);

    private static LeaveApprovalService Approval(DbSession s, ICurrentUserService user) => new(
        new LeaveRequestRepository(s), new LeaveBalanceRepository(s), new ScheduleRepository(s), new AttendanceLogRepository(s),
        new SystemSettingRepository(s), Checker(s), s, user, new FixedClock(), NullLogger<LeaveApprovalService>.Instance);

    private static LeaveBalanceService Balances(DbSession s, ICurrentUserService user) => new(
        new LeaveBalanceRepository(s), new LeaveBalanceAdjustmentRepository(s), new UserRepository(s), new SystemSettingRepository(s),
        s, user, new FixedClock());

    private static ReviewLeaveRequest Approve(string? note = null) => new() { Approved = true, Note = note };
    private static ReviewLeaveRequest Reject(string? note) => new() { Approved = false, Note = note };

    private static async Task<Schedule> ReloadAsync(DbSession s, Schedule schedule) =>
        (await new ScheduleRepository(s).GetByIdAsync(schedule.Id))!;

    // ------------------------------------------------------------------ Nghỉ

    [SkippableFact]
    public async Task Approved_leave_deducts_balance_and_puts_shifts_on_leave()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var morning = await NewShiftAsync(s, 8, 12);
        var day1 = await NewScheduleAsync(s, employee, morning, Day);
        var day2 = await NewScheduleAsync(s, employee, morning, Day.AddDays(1));
        var later = await NewScheduleAsync(s, employee, morning, Day.AddDays(5));
        var request = await As(s, employee).CreateAsync(Leave(AnnualLeave, Day, Day.AddDays(1)), null);
        Assert.True((await Service(s, Manager1).GetByIdAsync(request.Id)).CanReview);
        Assert.False((await As(s, employee).GetByIdAsync(request.Id)).CanReview);

        var result = await Approval(s, Manager1).ReviewAsync(request.Id, Approve("Đã sắp xếp người thay"));
        Assert.Equal(RequestStatus.Approved, result.Request.Status);
        Assert.Equal(ManagerId, result.Request.ApprovedBy);
        Assert.Equal(Now, result.Request.ApprovedAt);
        Assert.Equal("Đã sắp xếp người thay", result.Request.ApproverNote);
        Assert.False(result.Request.CanReview);
        Assert.Contains("trừ 2 ngày phép năm 2034", result.Message);
        Assert.Contains("2 ca chuyển sang nghỉ phép", result.Message);

        Assert.Equal(ScheduleStatus.OnLeave, (await ReloadAsync(s, day1)).Status);
        Assert.Equal(ScheduleStatus.OnLeave, (await ReloadAsync(s, day2)).Status);
        Assert.Equal(ScheduleStatus.Scheduled, (await ReloadAsync(s, later)).Status);
        var balance = await As(s, employee).GetMyBalanceAsync(2034);
        Assert.Equal((12m, 2m, 10m, 0m, false), (balance.TotalDays, balance.UsedDays, balance.RemainingDays, balance.PendingDays, balance.IsDefault));

        await Assert.ThrowsAsync<BadRequestException>(() => Approval(s, Hr).ReviewAsync(request.Id, Approve())); // đã xử lý
        await Assert.ThrowsAsync<BadRequestException>(() => As(s, employee).CancelAsync(request.Id)); // đã duyệt thì không rút

        // Xếp thêm ca vào ngày đã duyệt nghỉ → bị chặn
        var schedules = new ScheduleService(new ScheduleRepository(s), new ShiftRepository(s), new BranchRepository(s), new UserRepository(s),
            new LeaveRequestRepository(s), s, new TestUser(HrId, null, RoleNames.Hr), new FixedClock());
        var afternoon = await NewShiftAsync(s, 13, 17);
        var blocked = await Assert.ThrowsAsync<BadRequestException>(() => schedules.CreateAsync(new CreateSchedulesRequest
        {
            BranchId = Branch1,
            Items = new List<ScheduleItemRequest>
            {
                new() { UserId = employee.Id, ShiftId = afternoon.Id, WorkDate = DateOnly.FromDateTime(Day.AddDays(1)) }
            }
        }));
        Assert.Contains(blocked.Errors!, e => e.Contains("đã duyệt ngày 13/03/2034"));
    }

    [SkippableFact]
    public async Task Review_rules_scope_self_review_rejection_note_and_rollback()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var request = await As(s, employee).CreateAsync(Leave(AnnualLeave, Day.AddDays(10), Day.AddDays(12)), null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Approval(s, new TestUser(ManagerId, Branch2, RoleNames.Manager)).ReviewAsync(request.Id, Approve()));
        await Assert.ThrowsAsync<BadRequestException>(() => Approval(s, Manager1).ReviewAsync(request.Id, Reject(" ")));

        // Quỹ bị HR giảm sau khi gửi đơn → duyệt thất bại, không thay đổi gì (đơn vẫn chờ, quỹ chưa bị trừ)
        await Balances(s, Hr).AdjustAsync(new AdjustLeaveBalanceRequest { UserId = employee.Id, Year = 2034, DeltaDays = -10, Reason = "Đã nghỉ trước khi dùng hệ thống" });
        var shortage = await Assert.ThrowsAsync<BadRequestException>(() => Approval(s, Manager1).ReviewAsync(request.Id, Approve()));
        Assert.Contains("chỉ còn 2 ngày", shortage.Message);
        Assert.Equal(RequestStatus.Pending, (await As(s, employee).GetByIdAsync(request.Id)).Status);
        Assert.Equal(0m, (await As(s, employee).GetMyBalanceAsync(2034)).UsedDays);

        var rejected = await Approval(s, Manager1).ReviewAsync(request.Id, Reject("Cuối tháng kiểm kho, không bố trí được người"));
        Assert.Equal(RequestStatus.Rejected, rejected.Request.Status);
        Assert.Equal("Đã từ chối đơn.", rejected.Message);

        // Quản lý không tự duyệt đơn của mình — HR duyệt
        var manager = await NewEmployeeAsync(s);
        var managerUser = new TestUser(manager.Id, Branch1, RoleNames.Manager);
        var own = await Service(s, managerUser).CreateAsync(Leave(SickLeave, Day.AddDays(20), Day.AddDays(20)), Photo());
        Assert.False((await Service(s, managerUser).GetByIdAsync(own.Id)).CanReview);
        await Assert.ThrowsAsync<ForbiddenException>(() => Approval(s, managerUser).ReviewAsync(own.Id, Approve()));
        var byHr = await Approval(s, Hr).ReviewAsync(own.Id, Approve());
        Assert.Equal(RequestStatus.Approved, byHr.Request.Status);
        Assert.DoesNotContain("trừ", byHr.Message); // nghỉ ốm không trừ phép năm
    }

    // ------------------------------------------------------------------ Đổi ca

    [SkippableFact]
    public async Task Approved_swap_moves_shifts_and_conflicts_found_at_review_change_nothing()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var morning = await NewShiftAsync(s, 8, 12);
        var afternoon = await NewShiftAsync(s, 13, 17);
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s);
        var aMorning = await NewScheduleAsync(s, a, morning, Day);
        var bAfternoon = await NewScheduleAsync(s, b, afternoon, Day.AddDays(1));

        var swap = await As(s, a).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "Trùng lịch thi", SourceScheduleId = aMorning.Id, TargetScheduleId = bAfternoon.Id
        }, null);
        await Assert.ThrowsAsync<BadRequestException>(() => Approval(s, Manager1).ReviewAsync(swap.Id, Approve())); // chờ đồng nghiệp
        await As(s, b).RespondAsPeerAsync(swap.Id, new PeerResponseRequest { Accepted = true });

        var result = await Approval(s, Manager1).ReviewAsync(swap.Id, Approve());
        Assert.Equal(RequestStatus.Approved, result.Request.Status);
        Assert.Contains("Đã duyệt đổi ca", result.Message);
        Assert.Equal(b.Id, (await ReloadAsync(s, aMorning)).UserId);
        Assert.Equal(a.Id, (await ReloadAsync(s, bAfternoon)).UserId);

        // Nhờ làm thay: trong lúc chờ duyệt, người làm thay được xếp ca chồng giờ → duyệt thất bại, lịch giữ nguyên
        var c = await NewEmployeeAsync(s);
        var cMorning = await NewScheduleAsync(s, c, morning, Day.AddDays(2));
        var cover = await As(s, c).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "Đi khám bệnh", SourceScheduleId = cMorning.Id, TargetUserId = b.Id
        }, null);
        await As(s, b).RespondAsPeerAsync(cover.Id, new PeerResponseRequest { Accepted = true });
        await NewScheduleAsync(s, b, await NewShiftAsync(s, 10, 14), Day.AddDays(2));
        var conflict = await Assert.ThrowsAsync<BadRequestException>(() => Approval(s, Manager1).ReviewAsync(cover.Id, Approve()));
        Assert.Contains("Đồng nghiệp đã có", conflict.Message);
        Assert.Equal(c.Id, (await ReloadAsync(s, cMorning)).UserId);
        Assert.Equal(RequestStatus.Pending, (await As(s, c).GetByIdAsync(cover.Id)).Status);
    }

    // ------------------------------------------------------------------ OT & giải trình

    [SkippableFact]
    public async Task Overtime_is_rechecked_and_approved_explanation_validates_the_log()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        await NewScheduleAsync(s, employee, await NewShiftAsync(s, 8, 17), Day);
        CreateLeaveRequestForm Ot(int from, int to) => new()
        {
            LeaveTypeId = Overtime, Reason = "Kiểm kho", StartAt = Day.AddHours(from), EndAt = Day.AddHours(to)
        };

        var ot = await As(s, employee).CreateAsync(Ot(17, 19), null);
        var approved = await Approval(s, Manager1).ReviewAsync(ot.Id, Approve());
        Assert.Equal("Đã duyệt 2 giờ OT.", approved.Message);

        var late = await As(s, employee).CreateAsync(Ot(19, 21), null);
        await NewScheduleAsync(s, employee, await NewShiftAsync(s, 20, 23), Day); // Quản lý vừa xếp thêm ca tối
        await Assert.ThrowsAsync<BadRequestException>(() => Approval(s, Manager1).ReviewAsync(late.Id, Approve()));

        // Giải trình cho lượt chấm công đang chờ duyệt (không đọc được Wi-Fi) → lượt chấm công thành hợp lệ
        var yesterday = Now.Date.AddDays(-1);
        var schedule = await NewScheduleAsync(s, employee, await NewShiftAsync(s, 8, 12), yesterday);
        var log = new AttendanceLog
        {
            ScheduleId = schedule.Id, UserId = employee.Id, BranchId = Branch1, WorkDate = yesterday,
            CheckinTime = yesterday.AddHours(8).AddMinutes(12), CheckinImageUrl = "https://img.test/in.jpg",
            CheckinLat = 21.0316m, CheckinLng = 105.7831m, CheckinDistance = 10, CheckinFaceMatched = true,
            LateMinutes = 12, Status = AttendanceStatus.PendingApproval, Note = "Không xác định được Wi-Fi lúc check-in."
        };
        await new AttendanceLogRepository(s).AddAsync(log);
        var explanationType = (await new LeaveTypeRepository(s).GetListAsync(activeOnly: true)).First(t => t.Category == RequestCategory.Explanation).Id;
        var explanation = await As(s, employee).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = explanationType, Reason = "Điện thoại hết pin Wi-Fi, kẹt xe", AttendanceLogId = log.Id
        }, null);

        var result = await Approval(s, Manager1).ReviewAsync(explanation.Id, Approve("Camera xác nhận có mặt"));
        Assert.Contains("chuyển sang hợp lệ", result.Message);
        var updated = (await new AttendanceLogRepository(s).GetByIdAsync(log.Id))!;
        Assert.Equal(AttendanceStatus.Valid, updated.Status);
        Assert.Equal(ManagerId, updated.ReviewedBy);
        Assert.Equal("Chấp nhận đơn giải trình: Camera xác nhận có mặt", updated.ReviewNote);
    }

    // ------------------------------------------------------------------ Quỹ phép

    [SkippableFact]
    public async Task Hr_adjusts_balances_with_reasons_and_managers_only_read_their_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var employee = await NewEmployeeAsync(s);
        var hr = Balances(s, Hr);

        var row = Assert.Single((await hr.GetBalancesAsync(new LeaveBalanceFilter { Search = employee.EmployeeCode })).Items);
        Assert.Equal((2034, 12m, 12m, true), (row.Year, row.TotalDays, row.AvailableDays, row.IsDefault));

        var adjusted = await hr.AdjustAsync(new AdjustLeaveBalanceRequest { UserId = employee.Id, Year = 2034, DeltaDays = 2.5m, Reason = "Thâm niên 5 năm" });
        Assert.Equal((14.5m, 14.5m, false), (adjusted.TotalDays, adjusted.RemainingDays, adjusted.IsDefault));
        await As(s, employee).CreateAsync(Leave(AnnualLeave, Day.AddDays(30), Day.AddDays(31)), null);
        Assert.Equal(12.5m, (await hr.GetBalancesAsync(new LeaveBalanceFilter { Search = employee.EmployeeCode })).Items.Single().AvailableDays);

        AdjustLeaveBalanceRequest Delta(decimal days, int year = 2034, int? userId = null) =>
            new() { UserId = userId ?? employee.Id, Year = year, DeltaDays = days, Reason = "x" };
        await Assert.ThrowsAsync<BadRequestException>(() => hr.AdjustAsync(Delta(0)));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.AdjustAsync(Delta(0.3m)));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.AdjustAsync(Delta(31)));
        await Assert.ThrowsAsync<BadRequestException>(() => hr.AdjustAsync(Delta(-20))); // tổng âm
        await Assert.ThrowsAsync<BadRequestException>(() => hr.AdjustAsync(Delta(1, year: 2040)));
        await Assert.ThrowsAsync<ForbiddenException>(() => hr.AdjustAsync(Delta(1, userId: HrId)));

        var history = await hr.GetAdjustmentsAsync(employee.Id, 2034);
        var entry = Assert.Single(history);
        Assert.Equal((2.5m, "Thâm niên 5 năm", HrId), (entry.DeltaDays, entry.Reason, entry.CreatedBy));
        Assert.False(string.IsNullOrEmpty(entry.CreatedByName));

        // Quản lý chỉ xem chi nhánh mình; nhân viên chỉ xem lịch sử của mình
        var manager = Balances(s, Manager1);
        Assert.All((await manager.GetBalancesAsync(new LeaveBalanceFilter { PageSize = 100 })).Items, r => Assert.Equal(Branch1, r.BranchId));
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetBalancesAsync(new LeaveBalanceFilter { BranchId = Branch2 }));
        Assert.Single(await manager.GetAdjustmentsAsync(employee.Id, 2034));
        Assert.Single(await Balances(s, new TestUser(employee.Id, Branch1)).GetAdjustmentsAsync(employee.Id, 2034));
        var other = await NewEmployeeAsync(s);
        await Assert.ThrowsAsync<ForbiddenException>(() => Balances(s, new TestUser(other.Id, Branch1)).GetAdjustmentsAsync(employee.Id, 2034));
    }
}
