using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Notifications;
using FW.Application.Features.Timesheets;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Requests;

/// <summary>Quản lý / HR duyệt đơn (UC 5.3).</summary>
public class ReviewLeaveRequest
{
    [Required(ErrorMessage = "Vui lòng chọn duyệt hoặc từ chối.")]
    public bool? Approved { get; set; }

    /// <summary>Bắt buộc khi từ chối.</summary>
    [MaxLength(RequestRules.MaxNoteLength, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

/// <param name="Message">Tóm tắt các thay đổi đã áp dụng (trừ phép, số ca chuyển sang nghỉ, đổi người làm ca...).</param>
public record ReviewResultDto(LeaveRequestDto Request, string Message);

public interface ILeaveApprovalService
{
    Task<ReviewResultDto> ReviewAsync(int id, ReviewLeaveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Duyệt đơn (Task 4.2, UC 5.3 – 5.4). Quản lý duyệt đơn chi nhánh mình, Admin/HR duyệt mọi đơn; không ai tự duyệt đơn của mình.
/// Duyệt = một transaction gồm kết quả duyệt và thay đổi kèm theo, lỗi ở bất kỳ bước nào thì không thay đổi gì:
/// <list type="bullet">
/// <item>Nghỉ: trừ quỹ phép năm (loại trừ phép; chưa có quỹ thì tạo theo số ngày mặc định), các ca chưa chấm công trong những ngày
/// nghỉ chuyển sang <c>on_leave</c>.</item>
/// <item>Đổi ca: kiểm tra lại lịch rồi chuyển người làm của từng ca (đổi 2 chiều hoặc làm thay).</item>
/// <item>OT: kiểm tra lại không trùng ca đã xếp (Quản lý có thể đã xếp thêm ca trong lúc chờ duyệt); giờ OT được tính ở bảng công.</item>
/// <item>Giải trình: lượt chấm công chờ duyệt / bị từ chối chuyển sang hợp lệ; đi muộn / về sớm được miễn xử lý vi phạm ở bảng công.</item>
/// </list>
/// Từ chối: phải ghi lý do, không thay đổi gì khác.
/// </summary>
public class LeaveApprovalService : ILeaveApprovalService
{
    public const decimal MaxTotalLeaveDays = 99.9m; // giới hạn của cột DECIMAL(3, 1)

    private readonly ILeaveRequestRepository _requests;
    private readonly INotifier _notifier;
    private readonly ITimesheetGuard _guard;
    private readonly ILeaveBalanceRepository _balances;
    private readonly IScheduleRepository _schedules;
    private readonly IAttendanceLogRepository _logs;
    private readonly ISystemSettingRepository _settings;
    private readonly IRequestScheduleChecker _checker;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LeaveApprovalService> _logger;

    public LeaveApprovalService(
        ILeaveRequestRepository requests,
        INotifier notifier,
        ITimesheetGuard guard,
        ILeaveBalanceRepository balances,
        IScheduleRepository schedules,
        IAttendanceLogRepository logs,
        ISystemSettingRepository settings,
        IRequestScheduleChecker checker,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<LeaveApprovalService> logger)
    {
        _requests = requests;
        _notifier = notifier;
        _guard = guard;
        _balances = balances;
        _schedules = schedules;
        _logs = logs;
        _settings = settings;
        _checker = checker;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ReviewResultDto> ReviewAsync(int id, ReviewLeaveRequest review, CancellationToken cancellationToken = default)
    {
        var me = _currentUser.UserId ?? throw new UnauthorizedException();
        var request = await _requests.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Đơn", id);
        if (!RequestAccess.CanManage(_currentUser, request.BranchId))
        {
            throw new ForbiddenException("Đơn này không thuộc chi nhánh bạn quản lý.");
        }
        if (request.UserId == me) throw new ForbiddenException("Bạn không thể tự duyệt đơn của mình.");
        if (request.Status == RequestStatus.PendingPeer)
        {
            throw new BadRequestException("Đơn đổi ca đang chờ đồng nghiệp đồng ý — chưa duyệt được.");
        }
        if (request.Status != RequestStatus.Pending) throw new BadRequestException("Đơn này đã được xử lý.");

        var approved = review.Approved ?? throw new BadRequestException("Vui lòng chọn duyệt hoặc từ chối.");
        var note = string.IsNullOrWhiteSpace(review.Note) ? null : review.Note.Trim();
        if (!approved && note is null) throw new BadRequestException("Vui lòng ghi lý do từ chối.");

        var now = _clock.Now;
        string message;
        if (!approved)
        {
            await _unitOfWork.ExecuteInTransactionAsync(ct => CompleteAsync(request.Id, RequestStatus.Rejected, me, now, note, ct),
                cancellationToken);
            message = "Đã từ chối đơn.";
        }
        else
        {
            // Duyệt làm thay đổi số liệu bảng công của các tháng đụng tới — chặn nếu đã khóa
            await _guard.EnsureEditableAsync(request.UserId, request.StartDate, request.EndDate, cancellationToken);
            if (request.TargetUserId is { } colleagueId)
            {
                await _guard.EnsureEditableAsync(colleagueId, request.StartDate, request.EndDate, cancellationToken);
            }
            message = request.Category switch
            {
                RequestCategory.Leave => await ApproveLeaveAsync(request, me, now, note, cancellationToken),
                RequestCategory.ShiftSwap => await ApproveSwapAsync(request, me, now, note, cancellationToken),
                RequestCategory.Overtime => await ApproveOvertimeAsync(request, me, now, note, cancellationToken),
                RequestCategory.Explanation => await ApproveExplanationAsync(request, me, now, note, cancellationToken),
                _ => throw new BadRequestException("Loại đơn chưa được gán nhóm hợp lệ.")
            };
        }

        _logger.LogInformation("Người dùng {ReviewerId} {Action} đơn {RequestId} ({Category})",
            me, approved ? "duyệt" : "từ chối", request.Id, request.Category);
        var detail = await _requests.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Đơn", id);
        var result = LeaveRequestDto.From(detail, me, RequestAccess.CanReview(_currentUser, detail));

        var reference = result.Id.ToString();
        var reviewer = result.ApproverName ?? "Quản lý";
        var summary = $"{result.LeaveTypeName}: {RequestNotices.Describe(result)}";
        var reviewNote = note is null ? string.Empty : $" Ghi chú: {note}";
        await _notifier.NotifyAsync(new[] { result.Employee.Id }, NotificationType.LeaveApproval,
            approved ? "Đơn của bạn đã được duyệt" : "Đơn của bạn bị từ chối",
            $"{summary}. {(approved ? "Duyệt" : "Từ chối")} bởi {reviewer}.{reviewNote}", reference, cancellationToken);
        if (approved && result.Category == RequestCategory.ShiftSwap && result.TargetUser is { } colleague)
        {
            await _notifier.NotifyAsync(new[] { colleague.Id }, NotificationType.LeaveApproval, "Đổi ca đã được duyệt",
                $"{result.Employee.FullName}: {RequestNotices.Describe(result)}. Lịch của bạn đã được cập nhật.", reference, cancellationToken);
        }
        return new ReviewResultDto(result, message);
    }

    // ------------------------------------------------------------------ Từng nhóm đơn

    private async Task<string> ApproveLeaveAsync(LeaveRequestDetail request, int me, DateTime now, string? note, CancellationToken cancellationToken)
    {
        var year = request.StartDate.Year;
        var defaultDays = request.DeductsBalance ? await AnnualLeave.DefaultDaysAsync(_settings, cancellationToken) : 0;
        var marked = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (request.DeductsBalance)
            {
                await _balances.EnsureExistsAsync(request.UserId, year, defaultDays, ct);
                if (!await _balances.TryDeductAsync(request.UserId, year, request.DurationDays, ct))
                {
                    var balance = await _balances.GetByUserAndYearAsync(request.UserId, year, ct);
                    throw new BadRequestException(
                        $"Quỹ phép năm {year} của {request.FullName} chỉ còn {Days(balance?.RemainingDays ?? 0)} ngày, " +
                        $"không đủ {Days(request.DurationDays)} ngày — HR cần điều chỉnh quỹ phép trước khi duyệt.");
                }
            }
            marked = await _schedules.MarkOnLeaveAsync(request.UserId, request.StartDate, request.EndDate, ct);
            await CompleteAsync(request.Id, RequestStatus.Approved, me, now, note, ct);
        }, cancellationToken);

        var parts = new List<string> { $"Đã duyệt đơn nghỉ {Days(request.DurationDays)} ngày" };
        if (request.DeductsBalance) parts.Add($"trừ {Days(request.DurationDays)} ngày phép năm {year}");
        if (marked > 0) parts.Add($"{marked} ca chuyển sang nghỉ phép");
        return string.Join(", ", parts) + ".";
    }

    private async Task<string> ApproveSwapAsync(LeaveRequestDetail request, int me, DateTime now, string? note, CancellationToken cancellationToken)
    {
        var plan = await _checker.LoadSwapAsync(request.UserId, request.SourceScheduleId ?? 0, request.TargetScheduleId, request.TargetUserId,
            cancellationToken);
        await _checker.EnsureSwapFeasibleAsync(plan, excludeRequestId: request.Id, RequestViewpoint.Reviewer, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var moved = await _schedules.TryReassignAsync(plan.Source.Schedule.Id, request.UserId, plan.Colleague.Id, ct);
            if (moved && plan.Target is not null)
            {
                moved = await _schedules.TryReassignAsync(plan.Target.Schedule.Id, plan.Colleague.Id, request.UserId, ct);
            }
            if (!moved) throw new BadRequestException("Lịch vừa thay đổi (ca đã được chấm công hoặc chuyển người) — tải lại rồi thử lại.");
            await CompleteAsync(request.Id, RequestStatus.Approved, me, now, note, ct);
        }, cancellationToken);

        return plan.Target is null
            ? $"Đã duyệt: {plan.Colleague.FullName} làm thay {plan.Source.Label}."
            : $"Đã duyệt đổi ca: {request.FullName} nhận {plan.Target.Label}, {plan.Colleague.FullName} nhận {plan.Source.Label}.";
    }

    private async Task<string> ApproveOvertimeAsync(LeaveRequestDetail request, int me, DateTime now, string? note, CancellationToken cancellationToken)
    {
        if (request.StartAt is { } startAt && request.EndAt is { } endAt)
        {
            await _checker.EnsureOvertimeOutsideShiftsAsync(request.UserId, startAt, endAt, cancellationToken);
        }
        await _unitOfWork.ExecuteInTransactionAsync(ct => CompleteAsync(request.Id, RequestStatus.Approved, me, now, note, ct), cancellationToken);
        return $"Đã duyệt {Hours(request.DurationHours ?? 0)} giờ OT.";
    }

    private async Task<string> ApproveExplanationAsync(LeaveRequestDetail request, int me, DateTime now, string? note,
        CancellationToken cancellationToken)
    {
        var log = request.AttendanceLogId is { } logId ? await _logs.GetByIdAsync(logId, cancellationToken) : null;
        var validated = false;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (log is not null && log.Status != AttendanceStatus.Valid)
            {
                log.Status = AttendanceStatus.Valid;
                log.ReviewedBy = me;
                log.ReviewedAt = now;
                log.ReviewNote = Truncate(note is null ? "Chấp nhận đơn giải trình." : $"Chấp nhận đơn giải trình: {note}", RequestRules.MaxNoteLength);
                await _logs.UpdateAsync(log, ct);
                validated = true;
            }
            await CompleteAsync(request.Id, RequestStatus.Approved, me, now, note, ct);
        }, cancellationToken);

        return validated
            ? "Đã chấp nhận giải trình — lượt chấm công chuyển sang hợp lệ."
            : "Đã chấp nhận giải trình — đi muộn / về sớm của lượt này được miễn xử lý vi phạm.";
    }

    // ------------------------------------------------------------------

    private async Task CompleteAsync(int id, string status, int reviewerId, DateTime now, string? note, CancellationToken cancellationToken)
    {
        if (!await _requests.TryCompleteReviewAsync(id, status, reviewerId, now, note, cancellationToken))
        {
            throw new BadRequestException("Đơn vừa được người khác xử lý — tải lại để xem kết quả.");
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    private static string Days(decimal days) => days.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"));

    private static string Hours(decimal hours) => hours.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"));
}
