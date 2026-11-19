using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Application.Features.Notifications;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.Application.Features.Scheduling;

public interface IScheduleService
{
    /// <summary>Lịch chi nhánh trong khoảng ngày (gồm cả lịch nháp và lịch đã hủy).</summary>
    Task<IReadOnlyList<ScheduleDto>> GetBranchSchedulesAsync(ScheduleRangeQuery query, CancellationToken cancellationToken = default);

    /// <summary>Lịch đã công bố của người đang đăng nhập (mặc định 14 ngày từ hôm nay).</summary>
    Task<IReadOnlyList<ScheduleDto>> GetMySchedulesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    /// <summary>Xếp nhiều ca (dạng nháp). Có bất kỳ xung đột nào thì không xếp ca nào và trả về danh sách xung đột.</summary>
    Task<ScheduleBatchResult> CreateAsync(CreateSchedulesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sao chép lịch sang khoảng ngày khác (dạng nháp), bỏ qua các ca không còn hợp lệ hoặc bị xung đột.</summary>
    Task<ScheduleBatchResult> CopyAsync(CopySchedulesRequest request, CancellationToken cancellationToken = default);

    /// <returns>Số ca vừa được công bố.</returns>
    Task<int> PublishAsync(ScheduleRangeRequest request, CancellationToken cancellationToken = default);

    /// <returns>Số lịch nháp đã xóa.</returns>
    Task<int> DeleteDraftsAsync(ScheduleRangeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lịch nháp: xóa hẳn (trả true). Lịch đã công bố, chưa tới giờ: chuyển sang "đã hủy" (trả false).</summary>
    Task<bool> RemoveAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phân ca & xếp lịch (UC 3.2 – 3.4).
/// - Quản lý xếp lịch cho nhân viên thuộc chi nhánh mình; Admin/HR xếp cho mọi chi nhánh. Nhân viên phải thuộc chi nhánh
///   được xếp, ca phải là ca dùng chung hoặc ca riêng của chi nhánh đó, ngày làm từ hôm nay trở đi.
/// - Lịch mới là bản nháp: nhân viên chưa thấy, chưa chấm công được; công bố để chốt.
/// - Chặn trùng ca và chồng giờ (kể cả ca đêm hôm trước kéo sang sáng hôm sau) — xem <see cref="SchedulePlanner"/>.
/// - Không xếp ca vào ngày nhân viên nghỉ theo đơn đã duyệt (Task 4.2).
/// - Định biên: ca có số người tối đa (<see cref="Shift.MaxStaff"/>) thì mỗi chi nhánh chỉ xếp tới mức đó cho mỗi ngày. Việc đếm và ghi nằm trong
///   một transaction có khóa dòng của ca (<see cref="IShiftRepository.LockAsync"/>) để hai người xếp cùng lúc không vượt định biên.
/// </summary>
public class ScheduleService : IScheduleService
{
    public const int MaxItemsPerRequest = 500;
    public const int MaxRangeDays = 62;
    public const int MaxAdvanceDays = 366;
    public const int DefaultMyScheduleDays = 14;
    private const int MaxReportedIssues = 20;

    private readonly IScheduleRepository _schedules;
    private readonly IShiftRepository _shifts;
    private readonly IBranchRepository _branches;
    private readonly IUserRepository _users;
    private readonly ILeaveRequestRepository _leaveRequests;
    private readonly INotifier _notifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ScheduleService(
        IScheduleRepository schedules,
        IShiftRepository shifts,
        IBranchRepository branches,
        IUserRepository users,
        ILeaveRequestRepository leaveRequests,
        INotifier notifier,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _schedules = schedules;
        _shifts = shifts;
        _branches = branches;
        _users = users;
        _leaveRequests = leaveRequests;
        _notifier = notifier;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    // Xem lịch
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<ScheduleDto>> GetBranchSchedulesAsync(ScheduleRangeQuery query, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchId(query.BranchId);
        EnsureRange(query.FromDate, query.ToDate);

        var rows = await _schedules.GetDetailsAsync(new ScheduleQuery
        {
            BranchId = branchId,
            UserId = query.UserId,
            FromDate = ToDateTime(query.FromDate),
            ToDate = ToDateTime(query.ToDate)
        }, cancellationToken);
        return rows.Select(ScheduleDto.From).ToList();
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetMySchedulesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var from = fromDate ?? _clock.Today;
        var to = toDate ?? from.AddDays(DefaultMyScheduleDays - 1);
        EnsureRange(from, to);

        var rows = await _schedules.GetDetailsAsync(new ScheduleQuery
        {
            UserId = userId,
            PublishedOnly = true,
            FromDate = ToDateTime(from),
            ToDate = ToDateTime(to)
        }, cancellationToken);
        return rows.Select(ScheduleDto.From).ToList();
    }

    // ------------------------------------------------------------------
    // Xếp lịch
    // ------------------------------------------------------------------

    public async Task<ScheduleBatchResult> CreateAsync(CreateSchedulesRequest request, CancellationToken cancellationToken = default)
    {
        var branchId = await ResolveActiveBranchAsync(request.BranchId, cancellationToken);
        if (request.Items.Count == 0) throw new BadRequestException("Chưa chọn ca nào để xếp.");
        if (request.Items.Count > MaxItemsPerRequest) throw new BadRequestException($"Mỗi lần xếp tối đa {MaxItemsPerRequest} ca.");

        var context = await LoadContextAsync(branchId, request.Items.Select(i => i.UserId),
            request.Items.Min(i => i.WorkDate), request.Items.Max(i => i.WorkDate), cancellationToken);

        var errors = new List<string>();
        var items = new List<PlannedShift>();
        foreach (var item in request.Items)
        {
            var problem = context.Validate(item.UserId, item.ShiftId, item.WorkDate, _clock.Today);
            if (problem != null) errors.Add(problem);
            else items.Add(new PlannedShift(item.UserId, context.Shifts[item.ShiftId], ToDateTime(item.WorkDate)));
        }
        if (errors.Count > 0)
        {
            throw new BadRequestException("Không thể xếp lịch: dữ liệu chưa hợp lệ.", errors.Distinct().Take(MaxReportedIssues).ToList());
        }

        var plan = SchedulePlanner.Plan(items, await LoadExistingAsync(items, cancellationToken));
        if (plan.Conflicts.Count > 0)
        {
            throw new BadRequestException(
                $"Không thể xếp lịch: {plan.Conflicts.Count} ca bị trùng hoặc chồng giờ.",
                plan.Conflicts.Take(MaxReportedIssues).Select(c => Describe(c, context)).ToList());
        }

        await SaveAsync(branchId, plan.Accepted, context, allOrNothing: true, cancellationToken);
        return new ScheduleBatchResult(plan.Accepted.Count, 0, Array.Empty<string>());
    }

    public async Task<ScheduleBatchResult> CopyAsync(CopySchedulesRequest request, CancellationToken cancellationToken = default)
    {
        var branchId = await ResolveActiveBranchAsync(request.BranchId, cancellationToken);
        if (request.Days is < 1 or > 31) throw new BadRequestException("Số ngày sao chép từ 1 đến 31.");

        var offset = request.TargetFrom.DayNumber - request.SourceFrom.DayNumber;
        if (Math.Abs(offset) < request.Days)
        {
            throw new BadRequestException("Khoảng ngày đích không được trùng với khoảng ngày nguồn.");
        }

        var sourceTo = request.SourceFrom.AddDays(request.Days - 1);
        var source = (await _schedules.GetDetailsAsync(new ScheduleQuery
            {
                BranchId = branchId,
                FromDate = ToDateTime(request.SourceFrom),
                ToDate = ToDateTime(sourceTo)
            }, cancellationToken))
            .Where(s => s.Status != ScheduleStatus.Cancelled)
            .ToList();
        if (source.Count == 0)
        {
            throw new BadRequestException("Khoảng ngày nguồn chưa có lịch nào để sao chép.");
        }

        var context = await LoadContextAsync(branchId, source.Select(s => s.UserId),
            request.TargetFrom, request.TargetFrom.AddDays(request.Days - 1), cancellationToken);

        var skipped = new List<string>();
        var items = new List<PlannedShift>();
        foreach (var s in source)
        {
            var targetDate = DateOnly.FromDateTime(s.WorkDate).AddDays(offset);
            var problem = context.Validate(s.UserId, s.ShiftId, targetDate, _clock.Today);
            if (problem != null) skipped.Add(problem);
            else items.Add(new PlannedShift(s.UserId, context.Shifts[s.ShiftId], ToDateTime(targetDate)));
        }

        var plan = SchedulePlanner.Plan(items, await LoadExistingAsync(items, cancellationToken));
        skipped.AddRange(plan.Conflicts.Select(c => Describe(c, context)));

        var created = plan.Accepted.Count;
        if (plan.Accepted.Count > 0)
        {
            var full = await SaveAsync(branchId, plan.Accepted, context, allOrNothing: false, cancellationToken);
            created -= full.Count;
            skipped.AddRange(full.Select(r => Describe(r, context)));
        }
        return new ScheduleBatchResult(created, skipped.Count, skipped.Take(MaxReportedIssues).ToList());
    }

    // ------------------------------------------------------------------
    // Công bố / xóa / hủy
    // ------------------------------------------------------------------

    public async Task<int> PublishAsync(ScheduleRangeRequest request, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchId(request.BranchId);
        EnsureRange(request.FromDate, request.ToDate);
        var from = ToDateTime(request.FromDate);
        var to = ToDateTime(request.ToDate);
        var affected = await _schedules.GetDraftCountsByUserAsync(branchId, from, to, cancellationToken);
        var published = await _schedules.PublishAsync(branchId, from, to, _clock.Now, cancellationToken);

        // Báo từng nhân viên có ca mới (chỉ khi công bố được ít nhất một ca)
        if (published > 0 && affected.Count > 0)
        {
            await _notifier.NotifyAsync(affected.Keys, NotificationType.ScheduleNew, "Lịch làm việc mới",
                $"Lịch làm việc từ {request.FromDate:dd/MM} đến {request.ToDate:dd/MM} đã được công bố. Mở mục Lịch ca để xem.",
                request.FromDate.ToString("yyyy-MM-dd"), cancellationToken);
        }
        return published;
    }

    public async Task<int> DeleteDraftsAsync(ScheduleRangeRequest request, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchId(request.BranchId);
        EnsureRange(request.FromDate, request.ToDate);
        return await _schedules.DeleteDraftsAsync(branchId, ToDateTime(request.FromDate), ToDateTime(request.ToDate), cancellationToken);
    }

    public async Task<bool> RemoveAsync(long id, CancellationToken cancellationToken = default)
    {
        var schedule = await _schedules.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Lịch làm việc", id);
        _currentUser.EnsureCanAccessBranch(schedule.BranchId);

        if (!schedule.IsPublished)
        {
            await _schedules.DeleteAsync(id, cancellationToken);
            return true;
        }

        var shift = (await _shifts.GetByIdsAsync(new[] { schedule.ShiftId }, includeDeleted: true, cancellationToken)).Single();
        if (schedule.Status != ScheduleStatus.Scheduled || shift.StartAt(schedule.WorkDate) <= _clock.Now)
        {
            throw new BadRequestException("Chỉ hủy được ca đã công bố nhưng chưa tới giờ bắt đầu.");
        }

        schedule.Status = ScheduleStatus.Cancelled;
        await _schedules.UpdateAsync(schedule, cancellationToken);
        await _notifier.NotifyAsync(new[] { schedule.UserId }, NotificationType.ScheduleNew, "Ca làm việc bị hủy",
            $"{shift.Name} ngày {schedule.WorkDate:dd/MM} ({shift.StartTime:hh\\:mm}–{shift.EndTime:hh\\:mm}) đã bị Quản lý hủy.",
            schedule.WorkDate.ToString("yyyy-MM-dd"), cancellationToken);
        return false;
    }

    // ------------------------------------------------------------------
    // Hàm hỗ trợ
    // ------------------------------------------------------------------

    /// <summary>Quản lý: bỏ trống = chi nhánh của mình, chọn chi nhánh khác bị từ chối. Admin/HR: bắt buộc chọn chi nhánh.</summary>
    private int ResolveBranchId(int? requested)
    {
        var branchId = requested ?? _currentUser.GetBranchScope()
                       ?? throw new BadRequestException("Vui lòng chọn chi nhánh.");
        _currentUser.EnsureCanAccessBranch(branchId);
        return branchId;
    }

    private async Task<int> ResolveActiveBranchAsync(int? requested, CancellationToken cancellationToken)
    {
        var branchId = ResolveBranchId(requested);
        var branch = await _branches.GetByIdAsync(branchId, cancellationToken) ?? throw new NotFoundException("Chi nhánh", branchId);
        if (branch.Status != BranchStatus.Active)
        {
            throw new BadRequestException("Chi nhánh đã ngừng hoạt động, không thể xếp lịch.");
        }
        return branchId;
    }

    private static void EnsureRange(DateOnly from, DateOnly to)
    {
        if (from == default || to == default) throw new BadRequestException("Vui lòng chọn khoảng ngày.");
        if (to < from) throw new BadRequestException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw new BadRequestException($"Mỗi lần chỉ xem / thao tác tối đa {MaxRangeDays} ngày.");
        }
    }

    private async Task<SchedulingContext> LoadContextAsync(int branchId, IEnumerable<int> userIds, DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        var users = await _users.GetByIdsAsync(ids, cancellationToken);
        var shifts = await _shifts.GetAvailableForBranchAsync(branchId, cancellationToken);
        var leaves = await _leaveRequests.GetApprovedLeavesAsync(ids, ToDateTime(fromDate), ToDateTime(toDate), cancellationToken);
        return new SchedulingContext(branchId, users.ToDictionary(u => u.Id), shifts.ToDictionary(s => s.Id), leaves);
    }

    /// <summary>Lịch hiệu lực của các nhân viên liên quan, rộng hơn khoảng xếp 1 ngày mỗi phía (ca đêm kéo sang).</summary>
    private async Task<IReadOnlyList<PlannedShift>> LoadExistingAsync(IReadOnlyCollection<PlannedShift> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return Array.Empty<PlannedShift>();

        var existing = await _schedules.GetActiveByUsersAsync(
            items.Select(i => i.UserId),
            items.Min(i => i.Date).AddDays(-1),
            items.Max(i => i.Date).AddDays(1),
            cancellationToken);
        var shifts = (await _shifts.GetByIdsAsync(existing.Select(s => s.ShiftId), includeDeleted: true, cancellationToken))
            .ToDictionary(s => s.Id);

        return existing
            .Where(s => shifts.ContainsKey(s.ShiftId))
            .Select(s => new PlannedShift(s.UserId, shifts[s.ShiftId], s.WorkDate))
            .ToList();
    }

    /// <summary>
    /// Lưu các ca đã qua kiểm tra xung đột, đồng thời kiểm tra định biên trong CÙNG transaction (khóa ca → đếm người → ghi).
    /// <paramref name="allOrNothing"/> = true: có ca đã đủ người thì hoàn tác tất cả và báo lỗi; false: bỏ qua các ca đó và trả về danh sách.
    /// </summary>
    private async Task<IReadOnlyList<StaffingRejection>> SaveAsync(int branchId, IReadOnlyList<PlannedShift> accepted, SchedulingContext context,
        bool allOrNothing, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync<IReadOnlyList<StaffingRejection>>(async ct =>
        {
            var staffing = await PlanStaffingAsync(branchId, accepted, ct);
            if (allOrNothing && staffing.Rejected.Count > 0)
            {
                throw new BadRequestException(
                    $"Không thể xếp lịch: {staffing.Rejected.Count} ca đã đủ người theo định biên.",
                    staffing.Rejected.Take(MaxReportedIssues).Select(r => Describe(r, context)).ToList());
            }

            if (staffing.Allowed.Count > 0) await _schedules.AddRangeAsync(ToRows(branchId, staffing.Allowed), ct);
            return staffing.Rejected;
        }, cancellationToken);
    }

    /// <summary>Khóa các ca có định biên, đọc lại định biên mới nhất rồi đếm người đã xếp để biết ô nào còn chỗ.</summary>
    private async Task<StaffingPlan> PlanStaffingAsync(int branchId, IReadOnlyList<PlannedShift> accepted, CancellationToken cancellationToken)
    {
        var limited = accepted.Where(p => p.Shift.MaxStaff.HasValue).Select(p => p.Shift.Id).Distinct().ToList();
        if (limited.Count == 0) return new StaffingPlan(accepted, Array.Empty<StaffingRejection>());

        await _shifts.LockAsync(limited, cancellationToken);
        var fresh = (await _shifts.GetByIdsAsync(limited, includeDeleted: true, cancellationToken)).ToDictionary(s => s.Id);
        var items = accepted.Select(p => fresh.TryGetValue(p.Shift.Id, out var shift) ? p with { Shift = shift } : p).ToList();

        var slots = await _schedules.LockStaffedSlotsAsync(branchId, limited, items.Min(p => p.Date), items.Max(p => p.Date), cancellationToken);
        return StaffingPlanner.Plan(items, slots.Select(slot => (slot.ShiftId, slot.WorkDate)));
    }

    private List<Schedule> ToRows(int branchId, IEnumerable<PlannedShift> planned) => planned.Select(p => new Schedule
    {
        UserId = p.UserId,
        ShiftId = p.Shift.Id,
        BranchId = branchId,
        WorkDate = p.Date,
        Status = ScheduleStatus.Scheduled,
        IsPublished = false,
        CreatedBy = _currentUser.UserId
    }).ToList();

    private static string Describe(ScheduleConflict conflict, SchedulingContext context)
    {
        var who = context.Label(conflict.Item.UserId);
        var item = $"{conflict.Item.Date:dd/MM/yyyy} ca {ShiftLabel(conflict.Item.Shift)}";
        var source = conflict.WithIsExisting ? "đã có trong lịch" : "trong cùng lần xếp";

        return conflict.Kind == ScheduleConflictKind.Duplicate
            ? $"{who}: {item} bị trùng (ca này {source})."
            : $"{who}: {item} chồng giờ với ca {ShiftLabel(conflict.With.Shift)} ngày {conflict.With.Date:dd/MM/yyyy} ({source}).";
    }

    private static string Describe(StaffingRejection rejection, SchedulingContext context) =>
        $"{context.Label(rejection.Item.UserId)}: {rejection.Item.Date:dd/MM/yyyy} ca {ShiftLabel(rejection.Item.Shift)} đã đủ người " +
        $"({rejection.Current}/{rejection.Max}).";

    private static string ShiftLabel(Shift s) => $"{s.ShiftCode} ({ShiftTime.Format(s.StartTime)}–{ShiftTime.Format(s.EndTime)})";

    private static DateTime ToDateTime(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    /// <summary>Nhân viên và khung ca hợp lệ của chi nhánh, dùng để kiểm tra từng ca dự định xếp.</summary>
    private sealed record SchedulingContext(
        int BranchId, IReadOnlyDictionary<int, User> Users, IReadOnlyDictionary<int, Shift> Shifts, IReadOnlyList<ApprovedLeave> Leaves)
    {
        public string Label(int userId) =>
            Users.TryGetValue(userId, out var u) ? $"{u.EmployeeCode} – {u.FullName}" : $"Nhân viên #{userId}";

        /// <returns>Lý do không hợp lệ, hoặc null nếu hợp lệ.</returns>
        public string? Validate(int userId, int shiftId, DateOnly workDate, DateOnly today)
        {
            if (!Users.TryGetValue(userId, out var user)) return $"Nhân viên #{userId} không tồn tại.";
            var who = Label(userId);
            if (!user.IsActive) return $"{who}: tài khoản không còn hoạt động.";
            if (user.BranchId != BranchId) return $"{who}: không thuộc chi nhánh đang xếp lịch.";
            if (!Shifts.ContainsKey(shiftId)) return $"{who}: ca #{shiftId} không tồn tại hoặc không áp dụng cho chi nhánh này.";
            if (workDate < today) return $"{who}: không xếp được ca cho ngày đã qua ({workDate:dd/MM/yyyy}).";
            if (workDate.DayNumber - today.DayNumber > MaxAdvanceDays) return $"{who}: chỉ xếp lịch trước tối đa {MaxAdvanceDays} ngày.";
            var leave = Leaves.FirstOrDefault(l => l.UserId == userId && l.Covers(ToDateTime(workDate)));
            if (leave != null) return $"{who}: nghỉ theo đơn \"{leave.LeaveTypeName}\" đã duyệt ngày {workDate:dd/MM/yyyy}.";
            return null;
        }
    }
}
