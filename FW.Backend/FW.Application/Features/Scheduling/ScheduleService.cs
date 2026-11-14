using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ScheduleService(
        IScheduleRepository schedules,
        IShiftRepository shifts,
        IBranchRepository branches,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _schedules = schedules;
        _shifts = shifts;
        _branches = branches;
        _users = users;
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

        var context = await LoadContextAsync(branchId, request.Items.Select(i => i.UserId), cancellationToken);

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

        await SaveAsync(branchId, plan.Accepted, cancellationToken);
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

        var context = await LoadContextAsync(branchId, source.Select(s => s.UserId), cancellationToken);

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

        if (plan.Accepted.Count > 0) await SaveAsync(branchId, plan.Accepted, cancellationToken);
        return new ScheduleBatchResult(plan.Accepted.Count, skipped.Count, skipped.Take(MaxReportedIssues).ToList());
    }

    // ------------------------------------------------------------------
    // Công bố / xóa / hủy
    // ------------------------------------------------------------------

    public async Task<int> PublishAsync(ScheduleRangeRequest request, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchId(request.BranchId);
        EnsureRange(request.FromDate, request.ToDate);
        return await _schedules.PublishAsync(branchId, ToDateTime(request.FromDate), ToDateTime(request.ToDate), _clock.Now, cancellationToken);
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

    private async Task<SchedulingContext> LoadContextAsync(int branchId, IEnumerable<int> userIds, CancellationToken cancellationToken)
    {
        var users = await _users.GetByIdsAsync(userIds, cancellationToken);
        var shifts = await _shifts.GetAvailableForBranchAsync(branchId, cancellationToken);
        return new SchedulingContext(branchId, users.ToDictionary(u => u.Id), shifts.ToDictionary(s => s.Id));
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

    private async Task SaveAsync(int branchId, IReadOnlyList<PlannedShift> accepted, CancellationToken cancellationToken)
    {
        var rows = accepted.Select(p => new Schedule
        {
            UserId = p.UserId,
            ShiftId = p.Shift.Id,
            BranchId = branchId,
            WorkDate = p.Date,
            Status = ScheduleStatus.Scheduled,
            IsPublished = false,
            CreatedBy = _currentUser.UserId
        }).ToList();

        await _unitOfWork.ExecuteInTransactionAsync(ct => _schedules.AddRangeAsync(rows, ct), cancellationToken);
    }

    private static string Describe(ScheduleConflict conflict, SchedulingContext context)
    {
        var who = context.Label(conflict.Item.UserId);
        var item = $"{conflict.Item.Date:dd/MM/yyyy} ca {ShiftLabel(conflict.Item.Shift)}";
        var source = conflict.WithIsExisting ? "đã có trong lịch" : "trong cùng lần xếp";

        return conflict.Kind == ScheduleConflictKind.Duplicate
            ? $"{who}: {item} bị trùng (ca này {source})."
            : $"{who}: {item} chồng giờ với ca {ShiftLabel(conflict.With.Shift)} ngày {conflict.With.Date:dd/MM/yyyy} ({source}).";
    }

    private static string ShiftLabel(Shift s) => $"{s.ShiftCode} ({ShiftTime.Format(s.StartTime)}–{ShiftTime.Format(s.EndTime)})";

    private static DateTime ToDateTime(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    /// <summary>Nhân viên và khung ca hợp lệ của chi nhánh, dùng để kiểm tra từng ca dự định xếp.</summary>
    private sealed record SchedulingContext(int BranchId, IReadOnlyDictionary<int, User> Users, IReadOnlyDictionary<int, Shift> Shifts)
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
            return null;
        }
    }
}
