using System.Globalization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Requests;

/// <summary>Ảnh minh chứng gửi kèm đơn.</summary>
public record RequestAttachment(Stream Content, string FileName, string ContentType, long Length);

public interface ILeaveRequestService
{
    /// <summary>Tạo đơn (UC 5.1): nghỉ, đổi ca (chờ đồng nghiệp đồng ý), OT, giải trình.</summary>
    Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestForm form, RequestAttachment? attachment, CancellationToken cancellationToken = default);

    /// <summary>Đơn của tôi (UC 5.2).</summary>
    Task<PaginatedResult<LeaveRequestDto>> GetMineAsync(LeaveRequestFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Đơn đổi ca đồng nghiệp gửi tới tôi (UC 5.6).</summary>
    Task<PaginatedResult<LeaveRequestDto>> GetPeerRequestsAsync(LeaveRequestFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Đơn trong phạm vi quản lý: Admin/HR mọi chi nhánh, Quản lý chi nhánh mình.</summary>
    Task<PaginatedResult<LeaveRequestDto>> GetManagedAsync(LeaveRequestFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết: người tạo, đồng nghiệp được đề nghị đổi ca, người quản lý chi nhánh của đơn.</summary>
    Task<LeaveRequestDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Người tạo rút đơn khi chưa được duyệt.</summary>
    Task<LeaveRequestDto> CancelAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Đồng nghiệp đồng ý (đơn chuyển tới Quản lý duyệt) hoặc từ chối đổi ca (đơn kết thúc).</summary>
    Task<LeaveRequestDto> RespondAsPeerAsync(int id, PeerResponseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Quỹ phép năm của tôi (mặc định năm nay).</summary>
    Task<LeaveBalanceDto> GetMyBalanceAsync(int? year, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đơn từ (Task 4.1). Quy tắc thời gian / độ dài ở <see cref="RequestRules"/>. Ở đây kiểm tra các điều kiện cần CSDL:
/// đơn trùng, quỹ phép, quyền sở hữu ca / lượt chấm công, ca đổi có chồng giờ không. Duyệt đơn và các thay đổi kèm theo
/// (trừ phép, đổi lịch, cập nhật chấm công) thuộc Task 4.2.
/// </summary>
public class LeaveRequestService : ILeaveRequestService
{
    public const string StorageFolder = "requests";
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const decimal DefaultAnnualLeaveDays = 12;
    private static readonly IReadOnlyList<string> AllowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };

    private readonly ILeaveRequestRepository _requests;
    private readonly ILeaveTypeRepository _types;
    private readonly ILeaveBalanceRepository _balances;
    private readonly IScheduleRepository _schedules;
    private readonly IShiftRepository _shifts;
    private readonly IAttendanceLogRepository _logs;
    private readonly IUserRepository _users;
    private readonly ISystemSettingRepository _settings;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LeaveRequestService> _logger;

    public LeaveRequestService(
        ILeaveRequestRepository requests,
        ILeaveTypeRepository types,
        ILeaveBalanceRepository balances,
        IScheduleRepository schedules,
        IShiftRepository shifts,
        IAttendanceLogRepository logs,
        IUserRepository users,
        ISystemSettingRepository settings,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<LeaveRequestService> logger)
    {
        _requests = requests;
        _types = types;
        _balances = balances;
        _schedules = schedules;
        _shifts = shifts;
        _logs = logs;
        _users = users;
        _settings = settings;
        _storage = storage;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Tạo đơn

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestForm form, RequestAttachment? attachment,
        CancellationToken cancellationToken = default)
    {
        var me = await _users.GetByIdAsync(CurrentUserId, cancellationToken) ?? throw new UnauthorizedException();
        var type = form.LeaveTypeId is { } typeId ? await _types.GetByIdAsync(typeId, cancellationToken) : null;
        if (type is not { IsActive: true }) throw new BadRequestException("Loại đơn không tồn tại hoặc đã ngừng nhận đơn.");

        var reason = form.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0) throw new BadRequestException("Vui lòng nhập lý do.");
        if (reason.Length > RequestRules.MaxReasonLength) throw new BadRequestException("Lý do tối đa 1000 ký tự.");

        if (attachment is null && type.RequiresAttachment)
        {
            throw new BadRequestException($"Loại đơn \"{type.Name}\" cần kèm ảnh minh chứng.");
        }
        if (attachment is not null) ValidateImage(attachment);

        var request = new LeaveRequest { UserId = me.Id, LeaveTypeId = type.Id, Reason = reason, Status = RequestStatus.Pending };
        switch (type.Category)
        {
            case RequestCategory.Leave:
                await BuildLeaveAsync(request, form, type, me, cancellationToken);
                break;
            case RequestCategory.Overtime:
                await BuildOvertimeAsync(request, form, me, cancellationToken);
                break;
            case RequestCategory.ShiftSwap:
                await BuildSwapAsync(request, form, me, cancellationToken);
                break;
            case RequestCategory.Explanation:
                await BuildExplanationAsync(request, form, cancellationToken);
                break;
            default:
                throw new BadRequestException("Loại đơn chưa được gán nhóm hợp lệ — liên hệ HR.");
        }

        FileUploadResult? uploaded = null;
        if (attachment is not null)
        {
            uploaded = await _storage.UploadImageAsync(attachment.Content, attachment.FileName, StorageFolder, cancellationToken);
            request.AttachmentUrl = uploaded.Url;
            request.AttachmentPublicId = uploaded.PublicId;
        }

        try
        {
            await _requests.AddAsync(request, cancellationToken);
        }
        catch
        {
            if (uploaded is not null) await TryDeleteImageAsync(uploaded.PublicId);
            throw;
        }

        _logger.LogInformation("Người dùng {UserId} tạo đơn {RequestId} ({Category})", me.Id, request.Id, type.Category);
        return await GetDtoAsync(request.Id, cancellationToken);
    }

    private async Task BuildLeaveAsync(LeaveRequest request, CreateLeaveRequestForm form, LeaveType type, User me, CancellationToken cancellationToken)
    {
        if (form.StartDate is not { } start || form.EndDate is not { } end)
        {
            throw new BadRequestException("Vui lòng chọn ngày bắt đầu và ngày kết thúc nghỉ.");
        }
        Ensure(RequestRules.ValidateLeave(start, end, _clock.Today, type.DeductsBalance));

        var days = RequestRules.LeaveDays(start, end);
        if (await _requests.HasOverlappingLeaveAsync(me.Id, ToDate(start), ToDate(end), cancellationToken))
        {
            throw new BadRequestException("Bạn đã có đơn nghỉ (chờ duyệt hoặc đã duyệt) trùng với khoảng ngày này.");
        }
        if (type.DeductsBalance)
        {
            var balance = await GetBalanceAsync(me.Id, start.Year, cancellationToken);
            if (days > balance.AvailableDays)
            {
                var pending = balance.PendingDays > 0 ? $" (đã trừ {Days(balance.PendingDays)} ngày của đơn đang chờ duyệt)" : string.Empty;
                throw new BadRequestException(
                    $"Quỹ phép năm {start.Year} chỉ còn {Days(balance.AvailableDays)} ngày{pending}, không đủ cho {Days(days)} ngày nghỉ.");
            }
        }

        request.BranchId = me.BranchId;
        request.StartDate = ToDate(start);
        request.EndDate = ToDate(end);
        request.DurationDays = days;
    }

    private async Task BuildOvertimeAsync(LeaveRequest request, CreateLeaveRequestForm form, User me, CancellationToken cancellationToken)
    {
        if (form.StartAt is not { } startAt || form.EndAt is not { } endAt)
        {
            throw new BadRequestException("Vui lòng chọn giờ bắt đầu và giờ kết thúc làm thêm.");
        }
        startAt = TrimSeconds(startAt);
        endAt = TrimSeconds(endAt);
        Ensure(RequestRules.ValidateOvertime(startAt, endAt, _clock.Now));

        // OT là giờ làm NGOÀI ca: không được trùng ca đã xếp (nối tiếp ngay sau ca thì được)
        var mine = await LoadWithShiftsAsync(
            await _schedules.GetActiveByUserAsync(me.Id, startAt.Date.AddDays(-1), endAt.Date, cancellationToken), cancellationToken);
        var clash = mine.FirstOrDefault(s => RequestRules.Overlaps(startAt, endAt,
            s.Shift.StartAt(s.Schedule.WorkDate), s.Shift.EndAt(s.Schedule.WorkDate)));
        if (clash is not null)
        {
            throw new BadRequestException(
                $"Giờ OT trùng với {Label(clash)} — OT chỉ tính phần làm ngoài ca đã xếp.");
        }
        if (await _requests.HasOverlappingOvertimeAsync(me.Id, startAt, endAt, cancellationToken))
        {
            throw new BadRequestException("Bạn đã có đơn OT (chờ duyệt hoặc đã duyệt) trùng khoảng giờ này.");
        }

        request.BranchId = me.BranchId;
        request.StartDate = startAt.Date;
        request.EndDate = endAt.Date;
        request.StartAt = startAt;
        request.EndAt = endAt;
        request.DurationHours = RequestRules.OvertimeHours(startAt, endAt);
    }

    private async Task BuildSwapAsync(LeaveRequest request, CreateLeaveRequestForm form, User me, CancellationToken cancellationToken)
    {
        if (form.SourceScheduleId is not { } sourceId) throw new BadRequestException("Vui lòng chọn ca của bạn muốn đổi.");
        if (form.TargetScheduleId is null && form.TargetUserId is null)
        {
            throw new BadRequestException("Vui lòng chọn ca của đồng nghiệp muốn đổi, hoặc đồng nghiệp sẽ làm thay.");
        }

        var plan = await LoadSwapAsync(me.Id, sourceId, form.TargetScheduleId, form.TargetUserId, cancellationToken);
        await EnsureSwapFeasibleAsync(plan, excludeRequestId: null, cancellationToken);

        request.Status = RequestStatus.PendingPeer;
        request.BranchId = plan.Source.Schedule.BranchId;
        request.SourceScheduleId = plan.Source.Schedule.Id;
        request.TargetScheduleId = plan.Target?.Schedule.Id;
        request.TargetUserId = plan.Colleague.Id;
        var dates = new[] { plan.Source.Schedule.WorkDate.Date, plan.Target?.Schedule.WorkDate.Date ?? plan.Source.Schedule.WorkDate.Date };
        request.StartDate = dates.Min();
        request.EndDate = dates.Max();
    }

    private async Task BuildExplanationAsync(LeaveRequest request, CreateLeaveRequestForm form, CancellationToken cancellationToken)
    {
        if (form.AttendanceLogId is not { } logId) throw new BadRequestException("Vui lòng chọn lượt chấm công cần giải trình.");
        var log = await _logs.GetByIdAsync(logId, cancellationToken);
        if (log is null || log.UserId != CurrentUserId) throw new BadRequestException("Không tìm thấy lượt chấm công của bạn.");
        Ensure(RequestRules.ValidateExplanation(log, _clock.Today));
        if (await _requests.HasActiveExplanationAsync(log.Id, cancellationToken))
        {
            throw new BadRequestException("Lượt chấm công này đã có đơn giải trình (chờ duyệt hoặc đã duyệt).");
        }

        request.BranchId = log.BranchId;
        request.AttendanceLogId = log.Id;
        request.StartDate = log.WorkDate.Date;
        request.EndDate = log.WorkDate.Date;
    }

    // ------------------------------------------------------------------ Đổi ca

    private sealed record ScheduledShift(Schedule Schedule, Shift Shift);

    /// <param name="Target">Ca nhận lại; null = nhờ làm thay.</param>
    private sealed record SwapPlan(ScheduledShift Source, ScheduledShift? Target, User Colleague);

    private async Task<SwapPlan> LoadSwapAsync(int requesterId, long sourceId, long? targetScheduleId, int? targetUserId,
        CancellationToken cancellationToken)
    {
        var source = await LoadScheduleAsync(sourceId, cancellationToken);
        if (source is null || source.Schedule.UserId != requesterId) throw new BadRequestException("Không tìm thấy ca của bạn.");

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
        if (colleague is null || colleague.Status != UserStatus.Active) throw new BadRequestException("Đồng nghiệp không tồn tại hoặc đã ngừng làm việc.");

        return new SwapPlan(source, target, colleague);
    }

    /// <summary>Kiểm tra lúc tạo đơn và lúc đồng nghiệp đồng ý (lịch có thể đã thay đổi trong lúc chờ).</summary>
    private async Task EnsureSwapFeasibleAsync(SwapPlan plan, int? excludeRequestId, CancellationToken cancellationToken)
    {
        var now = _clock.Now;
        var (source, target, colleague) = plan;
        Ensure(RequestRules.ValidateSwappable(source.Schedule, source.Shift, now, "Ca của bạn"));

        if (target is not null)
        {
            Ensure(RequestRules.ValidateSwappable(target.Schedule, target.Shift, now, "Ca của đồng nghiệp"));
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

        // Đồng nghiệp nhận ca của tôi (đã bỏ ca đồng nghiệp nhường lại cho tôi)
        await EnsureCanTakeOverAsync(colleague.Id, source, release: target?.Schedule.Id, "Đồng nghiệp", cancellationToken);
        // Tôi nhận ca của đồng nghiệp (đã bỏ ca tôi nhường đi)
        if (target is not null)
        {
            await EnsureCanTakeOverAsync(source.Schedule.UserId, target, release: source.Schedule.Id, "Bạn", cancellationToken);
        }

        var involved = target is null ? new[] { source.Schedule.Id } : new[] { source.Schedule.Id, target.Schedule.Id };
        if (await _requests.HasOpenSwapForSchedulesAsync(involved, excludeRequestId, cancellationToken))
        {
            throw new BadRequestException("Ca này đang có một đơn đổi ca khác chờ xử lý.");
        }
    }

    private async Task EnsureCanTakeOverAsync(int userId, ScheduledShift incoming, long? release, string who, CancellationToken cancellationToken)
    {
        var date = incoming.Schedule.WorkDate.Date;
        var existing = (await LoadWithShiftsAsync(
                await _schedules.GetActiveByUserAsync(userId, date.AddDays(-1), date.AddDays(1), cancellationToken), cancellationToken))
            .Where(s => s.Schedule.Id != release)
            .Select(s => (s.Schedule, s.Shift));
        var conflict = RequestRules.FindTakeOverConflict(incoming.Shift, date, existing);
        if (conflict is { } c)
        {
            throw new BadRequestException(
                $"{who} đã có {Label(new ScheduledShift(c.Schedule, c.Shift))}, trùng hoặc chồng giờ với {Label(incoming)}.");
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

    private static string Label(ScheduledShift s) =>
        $"ca \"{s.Shift.Name}\" ngày {s.Schedule.WorkDate:dd/MM} ({s.Shift.StartTime:hh\\:mm}–{s.Shift.EndTime:hh\\:mm})";

    // ------------------------------------------------------------------ Xem, rút đơn, đồng nghiệp trả lời

    public async Task<PaginatedResult<LeaveRequestDto>> GetMineAsync(LeaveRequestFilter filter, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(filter);
        query.UserId = CurrentUserId;
        return await QueryAsync(query, cancellationToken);
    }

    public async Task<PaginatedResult<LeaveRequestDto>> GetPeerRequestsAsync(LeaveRequestFilter filter, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(filter);
        query.TargetUserId = CurrentUserId;
        query.Category = RequestCategory.ShiftSwap;
        return await QueryAsync(query, cancellationToken);
    }

    public async Task<PaginatedResult<LeaveRequestDto>> GetManagedAsync(LeaveRequestFilter filter, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(filter);
        query.UserId = filter.UserId;
        var scope = _currentUser.GetBranchScope(); // null = toàn doanh nghiệp
        query.BranchId = filter.BranchId ?? scope;
        if (query.BranchId.HasValue) _currentUser.EnsureCanAccessBranch(query.BranchId);
        return await QueryAsync(query, cancellationToken);
    }

    public async Task<LeaveRequestDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var detail = await LoadAsync(id, cancellationToken);
        var me = CurrentUserId;
        var canView = detail.UserId == me || detail.TargetUserId == me ||
                      _currentUser.HasCompanyWideAccess() || (_currentUser.IsBranchManager() && _currentUser.CanAccessBranch(detail.BranchId));
        if (!canView) throw new ForbiddenException("Bạn không có quyền xem đơn này.");
        return LeaveRequestDto.From(detail, me);
    }

    public async Task<LeaveRequestDto> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Đơn", id);
        if (request.UserId != CurrentUserId) throw new ForbiddenException("Chỉ người tạo đơn mới rút được đơn.");
        if (!RequestStatus.IsOpen(request.Status))
        {
            throw new BadRequestException("Chỉ rút được đơn đang chờ xử lý.");
        }

        request.Status = RequestStatus.Cancelled;
        await _requests.UpdateAsync(request, cancellationToken);
        return await GetDtoAsync(id, cancellationToken);
    }

    public async Task<LeaveRequestDto> RespondAsPeerAsync(int id, PeerResponseRequest response, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Đơn", id);
        if (request.TargetUserId != CurrentUserId || request.SourceScheduleId is null)
        {
            throw new ForbiddenException("Đơn này không phải đơn đổi ca gửi tới bạn.");
        }
        if (request.Status != RequestStatus.PendingPeer) throw new BadRequestException("Đơn không còn chờ bạn trả lời.");

        var accepted = response.Accepted ?? throw new BadRequestException("Vui lòng chọn đồng ý hoặc từ chối.");
        if (accepted)
        {
            // Lịch có thể đã đổi trong lúc chờ (ca bị hủy, đã qua giờ, đồng nghiệp được xếp thêm ca...)
            var plan = await LoadSwapAsync(request.UserId, request.SourceScheduleId.Value, request.TargetScheduleId, request.TargetUserId,
                cancellationToken);
            await EnsureSwapFeasibleAsync(plan, excludeRequestId: request.Id, cancellationToken);
        }

        request.Status = accepted ? RequestStatus.Pending : RequestStatus.PeerDeclined;
        request.PeerRespondedAt = _clock.Now;
        request.PeerNote = string.IsNullOrWhiteSpace(response.Note) ? null : response.Note.Trim();
        await _requests.UpdateAsync(request, cancellationToken);
        return await GetDtoAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ Quỹ phép

    public async Task<LeaveBalanceDto> GetMyBalanceAsync(int? year, CancellationToken cancellationToken = default) =>
        await GetBalanceAsync(CurrentUserId, year ?? _clock.Today.Year, cancellationToken);

    /// <summary>Chưa khởi tạo quỹ phép năm đó → dùng số ngày mặc định (system_settings.annual_leave_days) — Task 4.2 tạo bản ghi khi duyệt.</summary>
    private async Task<LeaveBalanceDto> GetBalanceAsync(int userId, int year, CancellationToken cancellationToken)
    {
        var balance = await _balances.GetByUserAndYearAsync(userId, year, cancellationToken);
        decimal total, used, remaining;
        if (balance is null)
        {
            var raw = await _settings.GetValueAsync(SystemSettingKeys.AnnualLeaveDays, cancellationToken);
            total = decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : DefaultAnnualLeaveDays;
            used = 0;
            remaining = total;
        }
        else
        {
            (total, used, remaining) = (balance.TotalDays, balance.UsedDays, balance.RemainingDays);
        }

        var pending = await _requests.GetPendingDeductibleDaysAsync(userId, year, cancellationToken);
        return new LeaveBalanceDto(year, total, used, remaining, pending, RequestRules.AvailableLeaveDays(remaining, pending), balance is null);
    }

    // ------------------------------------------------------------------ Tiện ích

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    private async Task<LeaveRequestDetail> LoadAsync(int id, CancellationToken cancellationToken) =>
        await _requests.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Đơn", id);

    private async Task<LeaveRequestDto> GetDtoAsync(int id, CancellationToken cancellationToken) =>
        LeaveRequestDto.From(await LoadAsync(id, cancellationToken), CurrentUserId);

    private async Task<PaginatedResult<LeaveRequestDto>> QueryAsync(LeaveRequestQuery query, CancellationToken cancellationToken)
    {
        var me = CurrentUserId;
        return (await _requests.GetDetailsPagedAsync(query, cancellationToken)).Map(d => LeaveRequestDto.From(d, me));
    }

    private static LeaveRequestQuery BuildQuery(LeaveRequestFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Status) && !RequestStatus.IsValid(filter.Status))
        {
            throw new BadRequestException("Trạng thái không hợp lệ (pending_peer, pending, approved, rejected, peer_declined, cancelled).");
        }
        if (!string.IsNullOrWhiteSpace(filter.Category) && !RequestCategory.IsValid(filter.Category))
        {
            throw new BadRequestException("Nhóm đơn không hợp lệ (leave, shift_swap, overtime, explanation).");
        }
        if (filter.FromDate > filter.ToDate) throw new BadRequestException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");

        return new LeaveRequestQuery
        {
            Statuses = string.IsNullOrWhiteSpace(filter.Status) ? null : new[] { filter.Status },
            Category = string.IsNullOrWhiteSpace(filter.Category) ? null : filter.Category,
            FromDate = filter.FromDate?.ToDateTime(TimeOnly.MinValue),
            ToDate = filter.ToDate?.ToDateTime(TimeOnly.MinValue),
            Search = filter.Search,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        };
    }

    private static void ValidateImage(RequestAttachment file)
    {
        if (file.Length <= 0) throw new BadRequestException("Ảnh minh chứng bị rỗng.");
        if (file.Length > MaxImageBytes) throw new BadRequestException("Ảnh minh chứng tối đa 5 MB.");
        if (!AllowedTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            throw new BadRequestException("Ảnh minh chứng phải là JPG, PNG, WEBP hoặc HEIC.");
        }
    }

    private async Task TryDeleteImageAsync(string publicId)
    {
        try
        {
            await _storage.DeleteAsync(publicId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được ảnh minh chứng {PublicId} sau khi tạo đơn thất bại", publicId);
        }
    }

    private static void Ensure(string? error)
    {
        if (error is not null) throw new BadRequestException(error);
    }

    private static DateTime ToDate(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    private static DateTime TrimSeconds(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    private static string Days(decimal days) => days.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"));
}
