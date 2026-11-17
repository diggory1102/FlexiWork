using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Application.Features.Notifications;
using FW.Domain.Enums;

namespace FW.Application.Features.Attendance;

public interface IAttendanceLogService
{
    /// <summary>Lịch sử chấm công của tôi (mặc định từ đầu tháng tới hôm nay).</summary>
    Task<PaginatedResult<AttendanceLogItemDto>> GetMineAsync(AttendanceLogFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu nhật ký chấm công (UC 4.5): Admin/HR mọi chi nhánh, Quản lý chi nhánh mình (mặc định 7 ngày gần nhất).</summary>
    Task<PaginatedResult<AttendanceLogItemDto>> GetLogsAsync(AttendanceLogFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một lượt chấm công: chính chủ hoặc người quản lý chi nhánh đó.</summary>
    Task<AttendanceLogDetailDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Chấp nhận / từ chối lượt chấm công đang chờ duyệt (UC 4.6).</summary>
    Task<AttendanceLogDetailDto> ReviewAsync(long id, ReviewAttendanceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tra cứu và duyệt nhật ký chấm công (Task 3.3). Phạm vi dữ liệu: nhân viên chỉ thấy bản ghi của mình; Quản lý thấy
/// chi nhánh mình; Admin/HR toàn doanh nghiệp. Không ai tự duyệt bản ghi của chính mình (bản ghi của Quản lý do HR/Admin duyệt).
/// </summary>
public class AttendanceLogService : IAttendanceLogService
{
    public const int MaxRangeDays = 366;
    public const int DefaultBranchRangeDays = 7;

    private readonly IAttendanceLogRepository _logs;
    private readonly INotifier _notifier;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public AttendanceLogService(IAttendanceLogRepository logs, INotifier notifier, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _logs = logs;
        _notifier = notifier;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PaginatedResult<AttendanceLogItemDto>> GetMineAsync(AttendanceLogFilter filter, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var query = BuildQuery(filter, defaultFrom: new DateOnly(today.Year, today.Month, 1), today);
        query.UserId = CurrentUserId;
        query.BranchId = null; // lịch sử cá nhân gồm mọi chi nhánh từng làm
        return (await _logs.GetDetailsPagedAsync(query, cancellationToken)).Map(AttendanceLogItemDto.From);
    }

    public async Task<PaginatedResult<AttendanceLogItemDto>> GetLogsAsync(AttendanceLogFilter filter, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var query = BuildQuery(filter, defaultFrom: today.AddDays(1 - DefaultBranchRangeDays), today);

        var scope = _currentUser.GetBranchScope(); // null = toàn doanh nghiệp
        query.BranchId = filter.BranchId ?? scope;
        if (query.BranchId.HasValue) _currentUser.EnsureCanAccessBranch(query.BranchId);

        return (await _logs.GetDetailsPagedAsync(query, cancellationToken)).Map(AttendanceLogItemDto.From);
    }

    public async Task<AttendanceLogDetailDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var log = await LoadAsync(id, cancellationToken);
        if (log.UserId != CurrentUserId && !_currentUser.CanAccessBranch(log.BranchId))
        {
            throw new ForbiddenException("Bạn không có quyền xem bản ghi chấm công này.");
        }
        return AttendanceLogDetailDto.From(log, CanReview(log));
    }

    public async Task<AttendanceLogDetailDto> ReviewAsync(long id, ReviewAttendanceRequest request, CancellationToken cancellationToken = default)
    {
        var log = await LoadAsync(id, cancellationToken);
        _currentUser.EnsureCanAccessBranch(log.BranchId);
        if (log.UserId == CurrentUserId)
        {
            throw new ForbiddenException("Bạn không thể tự duyệt bản ghi chấm công của mình.");
        }
        if (log.Status != AttendanceStatus.PendingApproval)
        {
            throw new BadRequestException("Bản ghi này không ở trạng thái chờ duyệt.");
        }

        var approved = request.Approved ?? throw new BadRequestException("Vui lòng chọn chấp nhận hoặc từ chối.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (!approved && note == null)
        {
            throw new BadRequestException("Vui lòng ghi lý do từ chối.");
        }

        log.Status = approved ? AttendanceStatus.Valid : AttendanceStatus.Invalid;
        log.ReviewedBy = CurrentUserId;
        log.ReviewedAt = _clock.Now;
        log.ReviewNote = note;
        await _logs.UpdateAsync(log, cancellationToken);

        var day = log.WorkDate.ToString("dd/MM");
        await _notifier.NotifyAsync(new[] { log.UserId }, NotificationType.Attendance,
            approved ? "Lượt chấm công đã được chấp nhận" : "Lượt chấm công bị từ chối",
            approved
                ? $"Lượt chấm công ngày {day} đã được chấp nhận và tính công.{(note is null ? string.Empty : $" Ghi chú: {note}")}"
                : $"Lượt chấm công ngày {day} bị từ chối: {note}. Lượt này không được tính công — bạn có thể gửi đơn giải trình.",
            log.Id.ToString(), cancellationToken);

        return AttendanceLogDetailDto.From(await LoadAsync(id, cancellationToken), canReview: false);
    }

    // ------------------------------------------------------------------

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    private async Task<AttendanceLogDetail> LoadAsync(long id, CancellationToken cancellationToken) =>
        await _logs.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bản ghi chấm công", id);

    private bool CanReview(AttendanceLogDetail log) =>
        log.Status == AttendanceStatus.PendingApproval &&
        log.UserId != CurrentUserId &&
        (_currentUser.HasCompanyWideAccess() || _currentUser.IsBranchManager()) &&
        _currentUser.CanAccessBranch(log.BranchId);

    private static AttendanceLogQuery BuildQuery(AttendanceLogFilter filter, DateOnly defaultFrom, DateOnly today)
    {
        // Mặc định: tới hôm nay; ngày bắt đầu mặc định (hoặc 7 ngày trước ngày kết thúc nếu ngày kết thúc nằm trước đó)
        var to = filter.ToDate ?? today;
        var from = filter.FromDate ?? (defaultFrom <= to ? defaultFrom : to.AddDays(1 - DefaultBranchRangeDays));

        if (to < from) throw new BadRequestException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw new BadRequestException($"Mỗi lần tra cứu tối đa {MaxRangeDays} ngày.");
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && !AttendanceStatus.IsValid(filter.Status))
        {
            throw new BadRequestException("Trạng thái không hợp lệ (valid, pending_approval, invalid).");
        }

        return new AttendanceLogQuery
        {
            FromDate = from.ToDateTime(TimeOnly.MinValue),
            ToDate = to.ToDateTime(TimeOnly.MinValue),
            UserId = filter.UserId,
            Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status,
            Search = filter.Search,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        };
    }
}
