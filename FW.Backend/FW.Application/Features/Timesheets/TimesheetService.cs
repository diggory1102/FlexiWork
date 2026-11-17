using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Application.Common.Settings;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Application.Features.Timesheets;

public interface ITimesheetService
{
    /// <summary>Bảng công của nhân viên trong tháng (Quản lý: chi nhánh mình; Admin/HR: tất cả).</summary>
    Task<PaginatedResult<TimesheetDto>> GetPagedAsync(TimesheetFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Bảng công của tôi: đã xác nhận / khóa thì lấy bản lưu, còn lại tính trực tiếp.</summary>
    Task<TimesheetDto> GetMineAsync(MyTimesheetFilter filter, CancellationToken cancellationToken = default);

    Task<TimesheetDetailDto> GetDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Tính (lại) bảng công nháp của tháng cho nhân viên trong phạm vi.</summary>
    Task<CalculateTimesheetsResult> CalculateAsync(CalculateTimesheetsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Quản lý xác nhận: nháp → đã xác nhận (kiểm tra lại số liệu mới nhất trước khi chốt).</summary>
    Task<ConfirmTimesheetsResult> ConfirmAsync(CalculateTimesheetsRequest request, CancellationToken cancellationToken = default);

    /// <summary>HR khóa: đã xác nhận → đã khóa, chỉ khi tháng đã kết thúc.</summary>
    Task<LockTimesheetsResult> LockAsync(CalculateTimesheetsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Mở lại về nháp (kèm lý do) để sửa số liệu nguồn rồi tính lại.</summary>
    Task<TimesheetDto> ReopenAsync(long id, ReopenTimesheetRequest request, CancellationToken cancellationToken = default);

    Task<TimesheetSummaryDto> GetSummaryAsync(int year, int month, int? branchId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tổng hợp bảng công tháng (Task 5.3, UC 8.2). Số liệu do <see cref="TimesheetCalculator"/> tính từ lịch đã công bố, chấm công, đơn nghỉ /
/// OT / giải trình đã duyệt. Quy trình trạng thái: <c>draft</c> (tính lại tùy ý) → <c>confirmed</c> (Quản lý) → <c>locked</c> (HR, chỉ khi tháng
/// đã kết thúc) → <c>finalized</c> (chốt lương, Task 5.5). Không ai tự xác nhận / mở lại bảng công của chính mình.
/// </summary>
public class TimesheetService : ITimesheetService
{
    private readonly ITimesheetRepository _timesheets;
    private readonly IScheduleRepository _schedules;
    private readonly IShiftRepository _shifts;
    private readonly IAttendanceLogRepository _logs;
    private readonly ILeaveRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly IPositionRepository _positions;
    private readonly IBranchRepository _branches;
    private readonly IAttendancePolicyService _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly AttendanceSettings _settings;
    private readonly ILogger<TimesheetService> _logger;

    public TimesheetService(
        ITimesheetRepository timesheets,
        IScheduleRepository schedules,
        IShiftRepository shifts,
        IAttendanceLogRepository logs,
        ILeaveRequestRepository requests,
        IUserRepository users,
        IPositionRepository positions,
        IBranchRepository branches,
        IAttendancePolicyService policy,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IOptions<AttendanceSettings> settings,
        ILogger<TimesheetService> logger)
    {
        _timesheets = timesheets;
        _schedules = schedules;
        _shifts = shifts;
        _logs = logs;
        _requests = requests;
        _users = users;
        _positions = positions;
        _branches = branches;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    private int Me => _currentUser.UserId ?? throw new UnauthorizedException();

    // ------------------------------------------------------------------ Truy vấn

    public async Task<PaginatedResult<TimesheetDto>> GetPagedAsync(TimesheetFilter filter, CancellationToken cancellationToken = default)
    {
        var (year, month) = ResolvePeriod(filter.Year, filter.Month);
        if (!string.IsNullOrWhiteSpace(filter.Status) && !TimesheetStatus.IsValid(filter.Status))
        {
            throw new BadRequestException("Trạng thái không hợp lệ (draft, confirmed, locked, finalized).");
        }

        var query = new TimesheetQuery
        {
            Year = year,
            Month = month,
            BranchId = ResolveBranch(filter.BranchId),
            Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status,
            Search = filter.Search,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        };
        return (await _timesheets.GetDetailsPagedAsync(query, cancellationToken)).Map(row => ToDto(row, isPreview: false, figures: null));
    }

    public async Task<TimesheetDto> GetMineAsync(MyTimesheetFilter filter, CancellationToken cancellationToken = default)
    {
        var (year, month) = ResolvePeriod(filter.Year, filter.Month);
        var me = Me;

        var stored = (await _timesheets.GetByUsersAsync(year, month, new[] { me }, cancellationToken)).FirstOrDefault();
        if (stored is not null && stored.Status != TimesheetStatus.Draft)
        {
            var row = await _timesheets.GetDetailByIdAsync(stored.Id, cancellationToken) ?? throw new NotFoundException("Bảng công", stored.Id);
            return ToDto(row, isPreview: false, figures: null);
        }

        var user = await _users.GetByIdAsync(me, cancellationToken) ?? throw new UnauthorizedException();
        var policy = await _policy.GetAsync(cancellationToken);
        var computed = (await ComputeAsync(year, month, new[] { me }, cancellationToken))[me];
        return await PreviewAsync(user, stored, computed, year, month, policy.StandardWorkDays, cancellationToken);
    }

    public async Task<TimesheetDetailDto> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await _timesheets.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bảng công", id);
        if (row.UserId != Me && !_currentUser.CanAccessBranch(row.BranchId))
        {
            throw new ForbiddenException("Bạn không có quyền xem bảng công này.");
        }

        var computed = (await ComputeAsync(row.Year, row.Month, new[] { row.UserId }, cancellationToken))[row.UserId];
        var live = computed.Result.Totals;
        var frozen = row.Status != TimesheetStatus.Draft;
        var drifted = frozen && TimesheetCalculator.Differs(ToTotals(row), live);

        var dto = ToDto(row, isPreview: !frozen, figures: frozen ? null : TimesheetFiguresDto.From(live));
        var lines = computed.Result.Lines.Select(ToLineDto).ToList();
        var overtime = computed.Overtime.Select(o => new OvertimeLineDto(o.StartAt, o.EndAt, o.Hours)).ToList();

        var canManage = row.UserId != Me && _currentUser.CanAccessBranch(row.BranchId);
        var canConfirm = canManage && row.Status == TimesheetStatus.Draft && live.PendingLogs == 0;
        var canLock = _currentUser.HasCompanyWideAccess() && row.UserId != Me && row.Status == TimesheetStatus.Confirmed && MonthEnded(row.Year, row.Month);
        var canReopen = row.UserId != Me && row.Status switch
        {
            TimesheetStatus.Confirmed => canManage,
            TimesheetStatus.Locked => _currentUser.HasCompanyWideAccess(),
            _ => false
        };
        return new TimesheetDetailDto(dto, TimesheetFiguresDto.From(live), drifted, lines, overtime, canConfirm, canLock, canReopen);
    }

    public async Task<TimesheetSummaryDto> GetSummaryAsync(int year, int month, int? branchId, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(year, month);
        var counts = await _timesheets.CountByStatusAsync(year, month, ResolveBranch(branchId), cancellationToken);
        int Count(string status) => counts.TryGetValue(status, out var value) ? value : 0;
        return new TimesheetSummaryDto(year, month, Count(TimesheetStatus.Draft), Count(TimesheetStatus.Confirmed), Count(TimesheetStatus.Locked),
            Count(TimesheetStatus.Finalized), MonthEnded(year, month));
    }

    // ------------------------------------------------------------------ Tính / xác nhận / khóa / mở lại

    public async Task<CalculateTimesheetsResult> CalculateAsync(CalculateTimesheetsRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(request.Year, request.Month);
        EnsureNotFuture(request.Year, request.Month);
        var branch = ResolveBranch(request.BranchId);
        var userIds = await UsersInScopeAsync(branch, request.UserIds, cancellationToken);

        var existing = (await _timesheets.GetByUsersAsync(request.Year, request.Month, userIds, cancellationToken)).ToDictionary(t => t.UserId);
        var skippedConfirmed = existing.Values.Count(t => t.Status == TimesheetStatus.Confirmed);
        var skippedFrozen = existing.Values.Count(t => TimesheetStatus.IsFrozen(t.Status));
        var targets = userIds.Where(id => !existing.TryGetValue(id, out var t) || t.Status == TimesheetStatus.Draft).ToList();

        var calculated = await RecalculateAsync(request.Year, request.Month, targets, existing, cancellationToken);
        var message = $"Đã tính {calculated} bảng công tháng {request.Month:00}/{request.Year}.";
        if (skippedConfirmed + skippedFrozen > 0)
        {
            message += $" Bỏ qua {skippedConfirmed} bảng đã xác nhận và {skippedFrozen} bảng đã khóa (mở lại để tính lại).";
        }
        _logger.LogInformation("Người dùng {UserId} tính bảng công {Month}/{Year}: {Count} bảng", Me, request.Month, request.Year, calculated);
        return new CalculateTimesheetsResult(request.Year, request.Month, calculated, skippedConfirmed, skippedFrozen, message);
    }

    public async Task<ConfirmTimesheetsResult> ConfirmAsync(CalculateTimesheetsRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(request.Year, request.Month);
        EnsureNotFuture(request.Year, request.Month);
        var me = Me;
        var branch = ResolveBranch(request.BranchId);
        var userIds = await UsersInScopeAsync(branch, request.UserIds, cancellationToken);

        var existing = (await _timesheets.GetByUsersAsync(request.Year, request.Month, userIds, cancellationToken)).ToDictionary(t => t.UserId);
        var candidates = userIds.Where(id => !existing.TryGetValue(id, out var t) || t.Status == TimesheetStatus.Draft).ToList();
        var computed = await ComputeAsync(request.Year, request.Month, candidates, cancellationToken);
        var names = (await _users.GetByIdsAsync(candidates, cancellationToken)).ToDictionary(u => u.Id, u => u.FullName);

        var blocked = new List<TimesheetBlockerDto>();
        var ready = new List<int>();
        foreach (var userId in candidates)
        {
            var totals = computed[userId].Result.Totals;
            if (totals.ScheduledShifts == 0 && totals.OvertimeHours == 0 && !existing.ContainsKey(userId)) continue; // không có dữ liệu để xác nhận
            var name = names.GetValueOrDefault(userId, $"#{userId}");
            if (userId == me) blocked.Add(new TimesheetBlockerDto(userId, name, "Bạn không thể tự xác nhận bảng công của mình."));
            else if (totals.PendingLogs > 0)
                blocked.Add(new TimesheetBlockerDto(userId, name, $"Còn {totals.PendingLogs} lượt chấm công chờ duyệt — duyệt xong mới xác nhận được."));
            else ready.Add(userId);
        }

        var confirmed = 0;
        var now = _clock.Now;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await RecalculateAsync(request.Year, request.Month, ready, existing, ct, computed);
            var rows = await _timesheets.GetByUsersAsync(request.Year, request.Month, ready, ct);
            foreach (var row in rows)
            {
                if (await _timesheets.TryConfirmAsync(row.Id, me, now, ct)) confirmed++;
            }
        }, cancellationToken);

        var message = $"Đã xác nhận {confirmed} bảng công.";
        if (blocked.Count > 0) message += $" {blocked.Count} bảng chưa xác nhận được.";
        _logger.LogInformation("Người dùng {UserId} xác nhận {Count} bảng công {Month}/{Year}", me, confirmed, request.Month, request.Year);
        return new ConfirmTimesheetsResult(confirmed, blocked, message);
    }

    public async Task<LockTimesheetsResult> LockAsync(CalculateTimesheetsRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(request.Year, request.Month);
        if (!_currentUser.HasCompanyWideAccess()) throw new ForbiddenException("Chỉ HR / Admin được khóa bảng công.");
        if (!MonthEnded(request.Year, request.Month))
        {
            throw new BadRequestException($"Tháng {request.Month:00}/{request.Year} chưa kết thúc — chỉ khóa được bảng công của tháng đã qua.");
        }

        var branch = ResolveBranch(request.BranchId);
        var userIds = request.UserIds is { Count: > 0 } ? request.UserIds.Distinct().ToList() : null;
        var locked = await _timesheets.LockConfirmedAsync(request.Year, request.Month, branch, userIds, Me, _clock.Now, cancellationToken);
        var counts = await _timesheets.CountByStatusAsync(request.Year, request.Month, branch, cancellationToken);
        var remaining = counts.GetValueOrDefault(TimesheetStatus.Draft);

        var message = $"Đã khóa {locked} bảng công tháng {request.Month:00}/{request.Year}.";
        if (remaining > 0) message += $" Còn {remaining} bảng nháp chưa xác nhận nên chưa khóa.";
        _logger.LogInformation("Người dùng {UserId} khóa {Count} bảng công {Month}/{Year}", Me, locked, request.Month, request.Year);
        return new LockTimesheetsResult(locked, remaining, message);
    }

    public async Task<TimesheetDto> ReopenAsync(long id, ReopenTimesheetRequest request, CancellationToken cancellationToken = default)
    {
        var me = Me;
        var row = await _timesheets.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bảng công", id);
        if (row.UserId == me) throw new ForbiddenException("Bạn không thể tự mở lại bảng công của mình.");

        switch (row.Status)
        {
            case TimesheetStatus.Confirmed:
                _currentUser.EnsureCanAccessBranch(row.BranchId);
                break;
            case TimesheetStatus.Locked:
                if (!_currentUser.HasCompanyWideAccess()) throw new ForbiddenException("Bảng công đã khóa — chỉ HR / Admin được mở lại.");
                break;
            case TimesheetStatus.Finalized:
                throw new BadRequestException("Bảng công đã chốt lương, không mở lại được.");
            default:
                throw new BadRequestException("Bảng công đang ở trạng thái nháp.");
        }

        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note)) throw new BadRequestException("Vui lòng ghi lý do mở lại bảng công.");
        if (!await _timesheets.TryReopenAsync(id, row.Status, note, cancellationToken))
        {
            throw new BadRequestException("Bảng công vừa được người khác thay đổi — tải lại để xem trạng thái mới.");
        }

        _logger.LogInformation("Người dùng {UserId} mở lại bảng công {Id} ({Status} → draft)", me, id, row.Status);
        var reopened = await _timesheets.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bảng công", id);
        return ToDto(reopened, isPreview: false, figures: null);
    }

    // ------------------------------------------------------------------ Tính toán

    private sealed record UserMonth(TimesheetResult Result, IReadOnlyList<OvertimeEntry> Overtime);

    /// <summary>Nạp dữ liệu nguồn của tháng rồi chạy <see cref="TimesheetCalculator"/> cho từng nhân viên (mỗi nhân viên đều có kết quả).</summary>
    private async Task<IReadOnlyDictionary<int, UserMonth>> ComputeAsync(int year, int month, IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, UserMonth>();
        var rules = await _policy.GetRulesAsync(cancellationToken);
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var schedules = await _schedules.GetPublishedAsync(from, to, userIds, cancellationToken);
        var shifts = (await _shifts.GetByIdsAsync(schedules.Select(s => s.ShiftId).Distinct().ToList(), includeDeleted: true, cancellationToken))
            .ToDictionary(s => s.Id);
        var logs = (await _logs.GetByScheduleIdsAsync(schedules.Select(s => s.Id).ToList(), cancellationToken))
            .GroupBy(l => l.ScheduleId).ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).Last());
        var exempted = (await _requests.GetApprovedExplanationLogIdsAsync(logs.Values.Select(l => l.Id).ToList(), cancellationToken)).ToHashSet();
        var leaves = (await _requests.GetApprovedLeavesAsync(userIds, from, to, cancellationToken)).ToLookup(l => l.UserId);
        var overtime = (await _requests.GetApprovedOvertimeAsync(from, to, userIds, cancellationToken)).ToLookup(o => o.UserId);
        var byUser = schedules.ToLookup(s => s.UserId);
        var now = _clock.Now;

        foreach (var userId in userIds)
        {
            var entries = new List<ShiftEntry>();
            foreach (var schedule in byUser[userId])
            {
                if (!shifts.TryGetValue(schedule.ShiftId, out var shift)) continue;
                logs.TryGetValue(schedule.Id, out var log);
                var leave = leaves[userId].FirstOrDefault(l => l.Covers(schedule.WorkDate));
                entries.Add(new ShiftEntry(schedule, shift, log, log is not null && exempted.Contains(log.Id),
                    leave is null ? null : leave.IsPaid ? LeaveKind.Paid : LeaveKind.Unpaid));
            }

            var otEntries = overtime[userId].Select(o => new OvertimeEntry(o.StartAt, o.EndAt, o.DurationHours)).ToList();
            results[userId] = new UserMonth(TimesheetCalculator.Calculate(entries, otEntries, rules, now, _settings.LateCheckOutMinutes), otEntries);
        }
        return results;
    }

    /// <summary>
    /// Tính và lưu bảng công nháp cho các nhân viên; nhân viên không có ca / OT và chưa có bảng công thì bỏ qua.
    /// </summary>
    /// <returns>Số bảng công đã lưu.</returns>
    private async Task<int> RecalculateAsync(int year, int month, IReadOnlyCollection<int> userIds, IReadOnlyDictionary<int, MonthlyTimesheet> existing,
        CancellationToken cancellationToken, IReadOnlyDictionary<int, UserMonth>? precomputed = null)
    {
        if (userIds.Count == 0) return 0;
        var computed = precomputed ?? await ComputeAsync(year, month, userIds, cancellationToken);
        var users = (await _users.GetByIdsAsync(userIds, cancellationToken)).ToDictionary(u => u.Id);
        var standardDays = (await _policy.GetAsync(cancellationToken)).StandardWorkDays;
        var now = _clock.Now;
        var saved = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var userId in userIds)
            {
                if (!users.TryGetValue(userId, out var user) || !computed.TryGetValue(userId, out var data)) continue;
                var totals = data.Result.Totals;
                if (totals.ScheduledShifts == 0 && totals.OvertimeHours == 0 && !existing.ContainsKey(userId)) continue;

                await _timesheets.UpsertDraftAsync(ToEntity(user, year, month, standardDays, totals, now), ct);
                saved++;
            }
        }, cancellationToken);
        return saved;
    }

    private static MonthlyTimesheet ToEntity(User user, int year, int month, decimal standardDays, TimesheetTotals t, DateTime now) => new()
    {
        UserId = user.Id,
        BranchId = user.BranchId,
        Year = year,
        Month = month,
        StandardWorkDays = standardDays,
        ScheduledShifts = t.ScheduledShifts,
        WorkedShifts = t.WorkedShifts,
        AbsentShifts = t.AbsentShifts,
        PendingLogs = t.PendingLogs,
        RejectedLogs = t.RejectedLogs,
        MissingCheckouts = t.MissingCheckouts,
        ActualWorkDays = t.ActualWorkDays,
        ActualWorkHours = t.ActualWorkHours,
        TotalLateMinutes = t.TotalLateMinutes,
        TotalEarlyMinutes = t.TotalEarlyMinutes,
        ViolationCount = t.ViolationCount,
        DeductedWorkDays = t.DeductedWorkDays,
        PenaltyAmount = t.PenaltyAmount,
        TotalOvertimeHours = t.OvertimeHours,
        PaidLeaveDays = t.PaidLeaveDays,
        UnpaidLeaveDays = t.UnpaidLeaveDays,
        NightShifts = t.NightShifts,
        SplitShiftDays = t.SplitShiftDays,
        CalculatedAt = now
    };

    private static TimesheetTotals ToTotals(MonthlyTimesheet t) => new(t.ScheduledShifts, t.WorkedShifts, t.AbsentShifts, t.PendingLogs, t.RejectedLogs,
        t.MissingCheckouts, t.ActualWorkDays, t.ActualWorkHours, t.TotalLateMinutes, t.TotalEarlyMinutes, t.ViolationCount, t.DeductedWorkDays,
        t.PenaltyAmount, t.TotalOvertimeHours, t.PaidLeaveDays, t.UnpaidLeaveDays, t.NightShifts, t.SplitShiftDays);

    // ------------------------------------------------------------------ Chuyển đổi DTO

    private static TimesheetDto ToDto(TimesheetRow row, bool isPreview, TimesheetFiguresDto? figures) => new(
        row.Id, new TimesheetEmployeeDto(row.UserId, row.EmployeeCode, row.FullName, row.AvatarUrl, row.PositionTitle), row.BranchId, row.BranchName,
        row.Year, row.Month, row.StandardWorkDays, row.Status, isPreview, figures ?? TimesheetFiguresDto.From(row), row.CalculatedSalary, row.CalculatedAt,
        row.ConfirmedByName, row.ConfirmedAt, row.LockedByName, row.LockedAt, row.Note);

    private async Task<TimesheetDto> PreviewAsync(User user, MonthlyTimesheet? draft, UserMonth data, int year, int month, decimal standardDays,
        CancellationToken cancellationToken)
    {
        var position = user.PositionId is { } positionId ? await _positions.GetByIdAsync(positionId, cancellationToken) : null;
        var branch = user.BranchId is { } branchId ? await _branches.GetByIdAsync(branchId, cancellationToken) : null;
        return new TimesheetDto(draft?.Id, new TimesheetEmployeeDto(user.Id, user.EmployeeCode, user.FullName, user.AvatarUrl, position?.Title),
            user.BranchId, branch?.Name, year, month, standardDays, TimesheetStatus.Draft, true, TimesheetFiguresDto.From(data.Result.Totals), 0,
            draft?.CalculatedAt, null, null, null, null, draft?.Note);
    }

    private static TimesheetLineDto ToLineDto(TimesheetLine l) => new(l.Schedule.Id, l.Schedule.WorkDate.Date, l.Shift.Name,
        l.Shift.StartTime.ToString(@"hh\:mm"), l.Shift.StartAt(l.Schedule.WorkDate).Add(l.Shift.Duration).ToString("HH:mm"), ToOutcomeCode(l.Outcome),
        l.Leave?.ToString().ToLowerInvariant(), l.WorkUnits, l.WorkHours, l.LateMinutes, l.EarlyMinutes, l.DeductedUnits, l.Penalty, l.Exempted, l.LogId,
        l.CheckIn, l.CheckOut);

    private static string ToOutcomeCode(ShiftOutcome outcome) => outcome switch
    {
        ShiftOutcome.Worked => "worked",
        ShiftOutcome.Absent => "absent",
        ShiftOutcome.Leave => "leave",
        ShiftOutcome.Pending => "pending",
        ShiftOutcome.Rejected => "rejected",
        ShiftOutcome.MissingCheckout => "missing_checkout",
        _ => "upcoming"
    };

    // ------------------------------------------------------------------ Phạm vi / kiểm tra

    /// <summary>Chi nhánh dùng để lọc: Quản lý mặc định chi nhánh mình (chọn chi nhánh khác bị từ chối); Admin/HR dùng chi nhánh đã chọn (null = tất cả).</summary>
    private int? ResolveBranch(int? requested)
    {
        var scope = _currentUser.GetBranchScope(); // null = toàn doanh nghiệp; cũng chặn người không có quyền quản lý
        var branch = requested ?? scope;
        if (branch.HasValue) _currentUser.EnsureCanAccessBranch(branch); // Quản lý chọn chi nhánh khác → từ chối, không âm thầm đổi
        return branch;
    }

    private async Task<List<int>> UsersInScopeAsync(int? branchId, IReadOnlyCollection<int>? requested, CancellationToken cancellationToken)
    {
        var ids = await _users.GetIdsByBranchAsync(branchId, cancellationToken);
        return requested is { Count: > 0 } ? ids.Where(requested.Contains).ToList() : ids.ToList();
    }

    private (int Year, int Month) ResolvePeriod(int? year, int? month)
    {
        var today = _clock.Today;
        var resolved = (Year: year ?? today.Year, Month: month ?? today.Month);
        ValidatePeriod(resolved.Year, resolved.Month);
        return resolved;
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new BadRequestException("Tháng / năm không hợp lệ.");
    }

    private void EnsureNotFuture(int year, int month)
    {
        var today = _clock.Today;
        if (year > today.Year || (year == today.Year && month > today.Month))
        {
            throw new BadRequestException($"Tháng {month:00}/{year} chưa tới — chưa tính bảng công được.");
        }
    }

    private bool MonthEnded(int year, int month) => new DateTime(year, month, 1).AddMonths(1) <= _clock.Now.Date;
}
