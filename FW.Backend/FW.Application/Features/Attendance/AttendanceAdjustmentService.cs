using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Application.Features.Notifications;
using FW.Application.Features.Timesheets;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.Application.Features.Attendance;

/// <summary>Quản lý / HR nhập giờ ra thay nhân viên quên check-out.</summary>
public class AdjustCheckoutRequest
{
    /// <summary>Giờ ra thực tế (giờ Việt Nam): sau giờ vào, không ở tương lai và không muộn quá 12 giờ sau khi ca kết thúc.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giờ ra.")]
    public DateTime? CheckoutTime { get; set; }

    [Required(ErrorMessage = "Vui lòng ghi lý do điều chỉnh.")]
    [MaxLength(255, ErrorMessage = "Lý do tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

public interface IAttendanceAdjustmentService
{
    /// <summary>
    /// Điều chỉnh giờ ra của lượt chấm công còn thiếu check-out: tính lại giờ làm và số phút về sớm như check-out thường, ghi người điều chỉnh
    /// và lý do, báo cho nhân viên. Không tự điều chỉnh bản ghi của mình; bị chặn khi bảng công của tháng đã khóa / chốt lương.
    /// </summary>
    Task<AttendanceLogDetailDto> AdjustCheckoutAsync(long logId, AdjustCheckoutRequest request, CancellationToken cancellationToken = default);
}

public class AttendanceAdjustmentService : IAttendanceAdjustmentService
{
    /// <summary>Giờ ra điều chỉnh được muộn nhất sau khi ca kết thúc (giờ).</summary>
    public const int MaxHoursAfterShiftEnd = 12;

    private readonly IAttendanceLogRepository _logs;
    private readonly IScheduleRepository _schedules;
    private readonly IShiftRepository _shifts;
    private readonly IUserRepository _users;
    private readonly INotifier _notifier;
    private readonly ITimesheetGuard _guard;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public AttendanceAdjustmentService(IAttendanceLogRepository logs, IScheduleRepository schedules, IShiftRepository shifts, IUserRepository users,
        INotifier notifier, ITimesheetGuard guard, IUnitOfWork unitOfWork, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _logs = logs;
        _schedules = schedules;
        _shifts = shifts;
        _users = users;
        _notifier = notifier;
        _guard = guard;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AttendanceLogDetailDto> AdjustCheckoutAsync(long logId, AdjustCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var me = _currentUser.UserId ?? throw new UnauthorizedException();
        var log = await _logs.GetByIdAsync(logId, cancellationToken) ?? throw new NotFoundException("Bản ghi chấm công", logId);
        _currentUser.EnsureCanAccessBranch(log.BranchId);
        if (log.UserId == me) throw new ForbiddenException("Bạn không thể tự điều chỉnh giờ ra của chính mình.");
        if (log.CheckoutTime.HasValue) throw new BadRequestException("Lượt chấm công này đã có giờ ra.");
        if (log.Status == AttendanceStatus.Invalid) throw new BadRequestException("Lượt chấm công đã bị từ chối nên không điều chỉnh giờ ra được.");

        var checkout = request.CheckoutTime ?? throw new BadRequestException("Vui lòng nhập giờ ra.");
        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note)) throw new BadRequestException("Vui lòng ghi lý do điều chỉnh.");

        var schedule = log.ScheduleId is { } scheduleId ? await _schedules.GetByIdAsync(scheduleId, cancellationToken) : null;
        var shift = schedule is null ? null : await _shifts.GetByIdAsync(schedule.ShiftId, cancellationToken);
        if (schedule is null || shift is null) throw new BadRequestException("Ca làm của lượt này đã bị xóa khỏi lịch nên không tính được giờ ra.");

        var latest = shift.EndAt(log.WorkDate).AddHours(MaxHoursAfterShiftEnd);
        if (checkout <= log.CheckinTime) throw new BadRequestException($"Giờ ra phải sau giờ vào ({log.CheckinTime:HH:mm dd/MM}).");
        if (checkout > _clock.Now) throw new BadRequestException("Giờ ra không được ở tương lai.");
        if (checkout > latest) throw new BadRequestException($"Giờ ra không được muộn quá {MaxHoursAfterShiftEnd} giờ sau khi ca kết thúc ({latest:HH:mm dd/MM}).");

        await _guard.EnsureEditableAsync(log.UserId, log.WorkDate, log.WorkDate, cancellationToken);

        log.CheckoutTime = checkout;
        log.EarlyMinutes = ViolationPolicy.EarlyLeaveMinutes(shift, log.WorkDate, checkout);
        log.ActualWorkHours = AttendanceRules.WorkedHours(shift, log.WorkDate, log.CheckinTime, checkout);
        log.CheckoutAdjustedBy = me;
        log.CheckoutAdjustedAt = _clock.Now;
        log.CheckoutAdjustNote = note;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _logs.UpdateAsync(log, ct);
            if (schedule.Status != ScheduleStatus.Completed)
            {
                schedule.Status = ScheduleStatus.Completed;
                await _schedules.UpdateAsync(schedule, ct);
            }
        }, cancellationToken);

        var adjuster = await _users.GetByIdAsync(me, cancellationToken);
        await _notifier.NotifyAsync(new[] { log.UserId }, NotificationType.Attendance, "Giờ ra đã được điều chỉnh",
            $"{adjuster?.FullName ?? "Quản lý"} đã ghi giờ ra {checkout:HH:mm} cho {shift.Name} ngày {log.WorkDate:dd/MM}. Lý do: {note}",
            log.Id.ToString(), cancellationToken);

        var detail = await _logs.GetDetailByIdAsync(logId, cancellationToken) ?? throw new NotFoundException("Bản ghi chấm công", logId);
        return AttendanceLogDetailDto.From(detail, canReview: false, canAdjustCheckout: false);
    }
}
