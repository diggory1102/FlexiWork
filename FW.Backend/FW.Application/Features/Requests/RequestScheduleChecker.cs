using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.Application.Features.Requests;

/// <summary>Một ca đã xếp kèm khung ca.</summary>
public sealed record ScheduledShift(Schedule Schedule, Shift Shift)
{
    public string Label => $"ca \"{Shift.Name}\" ngày {Schedule.WorkDate:dd/MM} ({Shift.StartTime:hh\\:mm}–{Shift.EndTime:hh\\:mm})";
}

/// <summary>Đơn đổi ca đã nạp: ca nhường đi, ca nhận lại (null = nhờ làm thay) và đồng nghiệp.</summary>
public sealed record SwapPlan(ScheduledShift Source, ScheduledShift? Target, User Colleague);

/// <summary>Ai đang thao tác — để câu báo lỗi xưng hô cho đúng.</summary>
public enum RequestViewpoint
{
    /// <summary>Nhân viên tạo đơn / đồng nghiệp trả lời.</summary>
    Requester,

    /// <summary>Quản lý / HR duyệt đơn.</summary>
    Reviewer
}

public interface IRequestScheduleChecker
{
    Task<SwapPlan> LoadSwapAsync(int requesterId, long sourceScheduleId, long? targetScheduleId, int? targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đổi ca còn thực hiện được không: 2 ca vẫn đã công bố, chưa bắt đầu, cùng chi nhánh; người nhận ca không trùng / chồng giờ
    /// và không đang nghỉ phép; không ca nào dính đơn đổi ca khác. Gọi khi tạo đơn, khi đồng nghiệp đồng ý và khi duyệt.
    /// </summary>
    Task EnsureSwapFeasibleAsync(SwapPlan plan, int? excludeRequestId, RequestViewpoint viewpoint, CancellationToken cancellationToken = default);

    /// <summary>OT là giờ làm ngoài ca: không được trùng ca đã xếp (nối tiếp ngay sau ca thì được).</summary>
    Task EnsureOvertimeOutsideShiftsAsync(int userId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gợi ý cho màn hình đổi ca: ca đã công bố, chưa bắt đầu của đồng nghiệp cùng chi nhánh trong ±7 ngày quanh ca muốn đổi,
    /// và đồng nghiệp đang làm việc ở chi nhánh đó (nhờ làm thay). Chưa kiểm tra chồng giờ — kiểm tra khi gửi đơn.
    /// </summary>
    Task<SwapOptionsDto> GetSwapOptionsAsync(int requesterId, long sourceScheduleId, CancellationToken cancellationToken = default);
}

/// <summary>Kiểm tra đơn đổi ca / OT với lịch làm việc hiện tại (Task 4.1, 4.2).</summary>
public class RequestScheduleChecker : IRequestScheduleChecker
{
    private readonly IScheduleRepository _schedules;
    private readonly IShiftRepository _shifts;
    private readonly IUserRepository _users;
    private readonly ILeaveRequestRepository _requests;
    private readonly IDateTimeProvider _clock;

    public RequestScheduleChecker(IScheduleRepository schedules, IShiftRepository shifts, IUserRepository users,
        ILeaveRequestRepository requests, IDateTimeProvider clock)
    {
        _schedules = schedules;
        _shifts = shifts;
        _users = users;
        _requests = requests;
        _clock = clock;
    }

    public async Task<SwapPlan> LoadSwapAsync(int requesterId, long sourceScheduleId, long? targetScheduleId, int? targetUserId,
        CancellationToken cancellationToken = default)
    {
        var source = await LoadScheduleAsync(sourceScheduleId, cancellationToken);
        if (source is null || source.Schedule.UserId != requesterId) throw new BadRequestException("Không tìm thấy ca của người tạo đơn.");

        ScheduledShift? target = null;
        if (targetScheduleId is { } targetId)
        {
            target = await LoadScheduleAsync(targetId, cancellationToken) ?? throw new BadRequestException("Không tìm thấy ca của đồng nghiệp.");
            if (targetUserId is { } given && given != target.Schedule.UserId)
            {
                throw new BadRequestException("Ca được chọn không thuộc đồng nghiệp này.");
            }
            targetUserId = target.Schedule.UserId;
        }

        if (targetUserId == requesterId) throw new BadRequestException("Không thể đổi ca với chính mình.");
        var colleague = await _users.GetByIdAsync(targetUserId!.Value, cancellationToken);
        if (colleague is null || colleague.Status != UserStatus.Active)
        {
            throw new BadRequestException("Đồng nghiệp không tồn tại hoặc đã ngừng làm việc.");
        }

        return new SwapPlan(source, target, colleague);
    }

    public async Task EnsureSwapFeasibleAsync(SwapPlan plan, int? excludeRequestId, RequestViewpoint viewpoint,
        CancellationToken cancellationToken = default)
    {
        var words = viewpoint == RequestViewpoint.Requester ? RequesterWords : ReviewerWords;
        var now = _clock.Now;
        var (source, target, colleague) = plan;
        Ensure(RequestRules.ValidateSwappable(source.Schedule, source.Shift, now, words.SourceShift));

        if (target is not null)
        {
            Ensure(RequestRules.ValidateSwappable(target.Schedule, target.Shift, now, words.TargetShift));
            if (target.Schedule.BranchId != source.Schedule.BranchId)
            {
                throw new BadRequestException("Chỉ đổi được với ca cùng chi nhánh.");
            }
            if (target.Schedule.ShiftId == source.Schedule.ShiftId && target.Schedule.WorkDate.Date == source.Schedule.WorkDate.Date)
            {
                throw new BadRequestException("Hai ca giống nhau (cùng ca, cùng ngày) — không cần đổi.");
            }
        }
        else if (colleague.BranchId != source.Schedule.BranchId)
        {
            throw new BadRequestException("Chỉ nhờ được đồng nghiệp cùng chi nhánh làm thay.");
        }

        // Đồng nghiệp nhận ca của người tạo đơn (đã bỏ ca đồng nghiệp nhường lại)
        await EnsureCanTakeOverAsync(colleague.Id, source, release: target?.Schedule.Id, words.Colleague, cancellationToken);
        // Người tạo đơn nhận ca của đồng nghiệp (đã bỏ ca mình nhường đi)
        if (target is not null)
        {
            await EnsureCanTakeOverAsync(source.Schedule.UserId, target, release: source.Schedule.Id, words.Requester, cancellationToken);
        }

        var involved = target is null ? new[] { source.Schedule.Id } : new[] { source.Schedule.Id, target.Schedule.Id };
        if (await _requests.HasOpenSwapForSchedulesAsync(involved, excludeRequestId, cancellationToken))
        {
            throw new BadRequestException("Ca này đang có một đơn đổi ca khác chờ xử lý.");
        }
    }

    public async Task EnsureOvertimeOutsideShiftsAsync(int userId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default)
    {
        var shifts = await LoadWithShiftsAsync(
            await _schedules.GetActiveByUserAsync(userId, startAt.Date.AddDays(-1), endAt.Date, cancellationToken), cancellationToken);
        var clash = shifts.FirstOrDefault(s => RequestRules.Overlaps(startAt, endAt,
            s.Shift.StartAt(s.Schedule.WorkDate), s.Shift.EndAt(s.Schedule.WorkDate)));
        if (clash is not null)
        {
            throw new BadRequestException($"Giờ OT trùng với {clash.Label} — OT chỉ tính phần làm ngoài ca đã xếp.");
        }
    }

    public const int SwapWindowDays = 7;

    public async Task<SwapOptionsDto> GetSwapOptionsAsync(int requesterId, long sourceScheduleId, CancellationToken cancellationToken = default)
    {
        var source = await LoadScheduleAsync(sourceScheduleId, cancellationToken);
        if (source is null || source.Schedule.UserId != requesterId) throw new BadRequestException("Không tìm thấy ca của bạn.");
        var now = _clock.Now;
        Ensure(RequestRules.ValidateSwappable(source.Schedule, source.Shift, now, "Ca của bạn"));

        var sourceDate = source.Schedule.WorkDate.Date;
        var from = sourceDate.AddDays(-SwapWindowDays) < now.Date ? now.Date : sourceDate.AddDays(-SwapWindowDays);
        var shifts = (await _schedules.GetDetailsAsync(new ScheduleQuery
            {
                BranchId = source.Schedule.BranchId, FromDate = from, ToDate = sourceDate.AddDays(SwapWindowDays), PublishedOnly = true
            }, cancellationToken))
            .Where(s => s.UserId != requesterId && s.Status == ScheduleStatus.Scheduled && s.WorkDate.Date + s.StartTime > now)
            .Where(s => !(s.ShiftId == source.Schedule.ShiftId && s.WorkDate.Date == sourceDate))
            .OrderBy(s => s.WorkDate).ThenBy(s => s.StartTime).ThenBy(s => s.FullName)
            .Select(s => new SwapShiftOptionDto(s.Id, s.UserId, s.EmployeeCode, s.FullName, s.WorkDate, s.ShiftName,
                s.StartTime.ToString(@"hh\:mm"), s.EndTime.ToString(@"hh\:mm")))
            .ToList();

        // Lấy đủ cả chi nhánh (không phân trang): người dùng tìm theo tên / mã ngay trên danh sách này
        var branchUserIds = await _users.GetIdsByBranchAsync(source.Schedule.BranchId, cancellationToken);
        var colleagues = (await _users.GetByIdsAsync(branchUserIds, cancellationToken))
            .Where(u => u.Id != requesterId && u.Status == UserStatus.Active)
            .OrderBy(u => u.FullName)
            .Select(u => new RequestPersonDto(u.Id, u.EmployeeCode, u.FullName, u.AvatarUrl))
            .ToList();

        var sourceDto = new RequestScheduleDto(source.Schedule.Id, source.Schedule.WorkDate, source.Shift.Name,
            source.Shift.StartTime.ToString(@"hh\:mm"), source.Shift.EndTime.ToString(@"hh\:mm"), source.Schedule.Status);
        return new SwapOptionsDto(sourceDto, shifts, colleagues);
    }

    // ------------------------------------------------------------------

    private sealed record Words(string SourceShift, string TargetShift, string Requester, string Colleague);

    private static readonly Words RequesterWords = new("Ca của bạn", "Ca của đồng nghiệp", "Bạn", "Đồng nghiệp");
    private static readonly Words ReviewerWords = new("Ca của người tạo đơn", "Ca của đồng nghiệp", "Người tạo đơn", "Đồng nghiệp");

    private async Task EnsureCanTakeOverAsync(int userId, ScheduledShift incoming, long? release, string who, CancellationToken cancellationToken)
    {
        var date = incoming.Schedule.WorkDate.Date;
        if ((await _requests.GetApprovedLeavesAsync(new[] { userId }, date, date, cancellationToken)).Count > 0)
        {
            throw new BadRequestException($"{who} được duyệt nghỉ ngày {date:dd/MM} nên không nhận {incoming.Label} được.");
        }

        var existing = (await LoadWithShiftsAsync(
                await _schedules.GetActiveByUserAsync(userId, date.AddDays(-1), date.AddDays(1), cancellationToken), cancellationToken))
            .Where(s => s.Schedule.Id != release)
            .Select(s => (s.Schedule, s.Shift));
        if (RequestRules.FindTakeOverConflict(incoming.Shift, date, existing) is { } c)
        {
            throw new BadRequestException(
                $"{who} đã có {new ScheduledShift(c.Schedule, c.Shift).Label}, trùng hoặc chồng giờ với {incoming.Label}.");
        }
    }

    private async Task<ScheduledShift?> LoadScheduleAsync(long id, CancellationToken cancellationToken)
    {
        var schedule = await _schedules.GetByIdAsync(id, cancellationToken);
        if (schedule is null) return null;
        var shift = (await _shifts.GetByIdsAsync(new[] { schedule.ShiftId }, includeDeleted: true, cancellationToken)).Single();
        return new ScheduledShift(schedule, shift);
    }

    private async Task<IReadOnlyList<ScheduledShift>> LoadWithShiftsAsync(IReadOnlyList<Schedule> schedules, CancellationToken cancellationToken)
    {
        if (schedules.Count == 0) return Array.Empty<ScheduledShift>();
        var shifts = (await _shifts.GetByIdsAsync(schedules.Select(s => s.ShiftId).Distinct(), includeDeleted: true, cancellationToken))
            .ToDictionary(s => s.Id);
        return schedules.Select(s => new ScheduledShift(s, shifts[s.ShiftId])).ToList();
    }

    private static void Ensure(string? error)
    {
        if (error is not null) throw new BadRequestException(error);
    }
}
