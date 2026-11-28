using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Enums;
using FW.Domain.Services;
using Req = FW.Application.Features.Requests;
using Ts = FW.Application.Features.Timesheets;

namespace FW.Application.Features.Assistant;

/// <summary>
/// Cài đặt <see cref="IHrDataService"/> bằng chính các service / repository của hệ thống — trợ lý không có đường tắt vào dữ liệu:
/// mọi hàm đều chỉ chạm tới dữ liệu của NGƯỜI ĐANG ĐĂNG NHẬP (kiểm tra <c>userId</c> khớp JWT) và dùng lại quy tắc phạm vi của service gốc.
/// </summary>
public sealed class HrDataService : IHrDataService
{
    private readonly ILeaveTypeRepository _types;
    private readonly IScheduleRepository _schedules;
    private readonly IAttendanceLogRepository _logs;
    private readonly IUserRepository _users;
    private readonly Req.ILeaveRequestService _requests;
    private readonly Ts.ITimesheetService _timesheets;
    private readonly ICurrentUserService _currentUser;

    public HrDataService(
        ILeaveTypeRepository types,
        IScheduleRepository schedules,
        IAttendanceLogRepository logs,
        IUserRepository users,
        Req.ILeaveRequestService requests,
        Ts.ITimesheetService timesheets,
        ICurrentUserService currentUser)
    {
        _types = types;
        _schedules = schedules;
        _logs = logs;
        _users = users;
        _requests = requests;
        _timesheets = timesheets;
        _currentUser = currentUser;
    }

    /// <summary>Chặn truy cập dữ liệu của người khác ngay cả khi tầng gọi truyền nhầm userId.</summary>
    private void EnsureSelf(int userId)
    {
        if (_currentUser.UserId != userId) throw new ForbiddenException("Trợ lý chỉ xem được dữ liệu của chính bạn.");
    }

    internal static RequestKind ToKind(string category) => category switch
    {
        RequestCategory.ShiftSwap => RequestKind.ShiftSwap,
        RequestCategory.Overtime => RequestKind.Overtime,
        RequestCategory.Explanation => RequestKind.Explanation,
        _ => RequestKind.Leave
    };

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken ct = default) =>
        (await _types.GetListAsync(activeOnly: true, ct))
        .Select(t => new LeaveTypeDto(t.Id, t.Name, t.IsPaid, t.RequiresAttachment, ToKind(t.Category), t.DeductsBalance))
        .ToList();

    private static ScheduledShiftDto ToShift(ScheduleDetail s) => new(
        s.Id, DateOnly.FromDateTime(s.WorkDate), s.ShiftId, s.ShiftName, s.StartTime, s.EndTime,
        1m, s.Status, s.BranchId); // WorkUnits chỉ dùng cho nghỉ nửa ngày — hệ thống không có

    public async Task<IReadOnlyList<ScheduledShiftDto>> GetMyScheduleAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureSelf(userId);
        var rows = await _schedules.GetDetailsAsync(new ScheduleQuery
        {
            FromDate = from.ToDateTime(TimeOnly.MinValue),
            ToDate = to.ToDateTime(TimeOnly.MinValue),
            UserId = userId,
            PublishedOnly = true
        }, ct);
        return rows.Where(r => r.Status != ScheduleStatus.Cancelled).Select(ToShift).ToList();
    }

    public async Task<TimesheetSummaryDto> GetMyTimesheetAsync(int userId, int year, int month, CancellationToken ct = default)
    {
        EnsureSelf(userId);
        var t = await _timesheets.GetMineAsync(new Ts.MyTimesheetFilter { Year = year, Month = month }, ct);
        var f = t.Figures;
        return new TimesheetSummaryDto(t.Year, t.Month, t.StandardWorkDays, f.ActualWorkDays, f.TotalLateMinutes, f.TotalEarlyMinutes,
            f.TotalOvertimeHours, f.PaidLeaveDays, f.UnpaidLeaveDays, t.IsPreview ? $"{t.Status} (tạm tính)" : t.Status);
    }

    public async Task<LeaveBalanceDto?> GetMyLeaveBalanceAsync(int userId, int year, CancellationToken ct = default)
    {
        EnsureSelf(userId);
        var b = await _requests.GetMyBalanceAsync(year, ct);
        return new LeaveBalanceDto(b.Year, b.TotalDays, b.UsedDays, b.RemainingDays, b.PendingDays);
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> GetMyRequestsAsync(int userId, string? status, int limit, CancellationToken ct = default)
    {
        EnsureSelf(userId);
        var page = await _requests.GetMineAsync(new Req.LeaveRequestFilter { Status = status, PageIndex = 1, PageSize = Math.Clamp(limit, 1, 100) }, ct);
        return page.Items.Select(r => new LeaveRequestDto(
            r.Id, r.LeaveTypeId, r.LeaveTypeName, ToKind(r.Category), DateOnly.FromDateTime(r.StartDate), DateOnly.FromDateTime(r.EndDate),
            r.DurationDays, r.Reason, r.Status, r.CreatedAt, r.ApproverNote)).ToList();
    }

    public async Task<IReadOnlyList<AttendanceLogDto>> GetMyAttendanceLogsAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureSelf(userId);
        var page = await _logs.GetDetailsPagedAsync(new AttendanceLogQuery
        {
            FromDate = from.ToDateTime(TimeOnly.MinValue),
            ToDate = to.ToDateTime(TimeOnly.MinValue),
            UserId = userId,
            PageIndex = 1,
            PageSize = 100
        }, ct);
        return page.Items
            .Where(l => l.Status != AttendanceStatus.Invalid)
            .OrderBy(l => l.CheckinTime)
            .Select(l => new AttendanceLogDto(
                l.Id, DateOnly.FromDateTime(l.WorkDate), l.ShiftName ?? "Ca làm", l.ShiftStartTime, l.CheckinTime, l.CheckoutTime,
                l.LateMinutes, l.EarlyMinutes, l.Status, RequestRules.NeedsExplanation(l)))
            .ToList();
    }

    public async Task<IReadOnlyList<ColleagueDto>> FindColleaguesAsync(int requesterId, string name, CancellationToken ct = default)
    {
        EnsureSelf(requesterId);
        var me = await _users.GetByIdAsync(requesterId, ct);
        if (me?.BranchId is not { } branchId || string.IsNullOrWhiteSpace(name)) return Array.Empty<ColleagueDto>();

        var wanted = RelativeDateResolver.Normalize(name);
        // Lọc theo tên (không dấu) trên TOÀN BỘ nhân viên đang làm của chi nhánh, không chỉ trang đầu
        var ids = await _users.GetActiveIdsByRoleAsync(RoleNames.Employee, branchId, ct);
        return (await _users.GetByIdsAsync(ids, ct))
            .Where(u => u.Id != requesterId && RelativeDateResolver.Normalize(u.FullName).Contains(wanted, StringComparison.Ordinal))
            .OrderBy(u => u.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .Select(u => new ColleagueDto(u.Id, u.FullName, u.EmployeeCode))
            .ToList();
    }

    public async Task<IReadOnlyList<ScheduledShiftDto>> GetColleagueShiftsForSwapAsync(
        int requesterId, int colleagueId, DateOnly date, CancellationToken ct = default)
    {
        EnsureSelf(requesterId);
        var users = await _users.GetByIdsAsync(new[] { requesterId, colleagueId }, ct);
        var me = users.FirstOrDefault(u => u.Id == requesterId);
        var colleague = users.FirstOrDefault(u => u.Id == colleagueId);
        // Chỉ trả ca khi cùng chi nhánh — không phải đường xem lịch người khác
        if (me?.BranchId is null || colleague is null || colleague.BranchId != me.BranchId) return Array.Empty<ScheduledShiftDto>();

        var rows = await _schedules.GetDetailsAsync(new ScheduleQuery
        {
            FromDate = date.ToDateTime(TimeOnly.MinValue),
            ToDate = date.ToDateTime(TimeOnly.MinValue),
            UserId = colleagueId,
            PublishedOnly = true
        }, ct);
        return rows.Where(r => r.Status == ScheduleStatus.Scheduled).Select(ToShift).ToList();
    }

    public async Task<IReadOnlyList<PendingRequestDto>> GetPendingRequestsAsync(int? branchId, int limit, CancellationToken ct = default)
    {
        // Service gốc tự giới hạn phạm vi theo vai trò (Quản lý chỉ thấy chi nhánh mình)
        var page = await _requests.GetManagedAsync(new Req.LeaveRequestFilter
        {
            Status = RequestStatus.Pending,
            BranchId = branchId,
            PageIndex = 1,
            PageSize = Math.Clamp(limit, 1, 100)
        }, ct);
        return page.Items.Select(r => new PendingRequestDto(
            r.Id, r.Employee.Id, r.Employee.FullName, r.LeaveTypeName, DateOnly.FromDateTime(r.StartDate), DateOnly.FromDateTime(r.EndDate),
            r.DurationDays, r.Reason, r.CreatedAt)).ToList();
    }
}

/// <summary>Gửi đơn thật qua <see cref="Req.ILeaveRequestService.CreateAsync"/> — dùng lại toàn bộ kiểm tra hợp lệ, trừ phép và thông báo cho Quản lý.</summary>
public sealed class LeaveRequestSubmitter : ILeaveRequestSubmitter
{
    private readonly Req.ILeaveRequestService _requests;
    private readonly ICurrentUserService _currentUser;

    public LeaveRequestSubmitter(Req.ILeaveRequestService requests, ICurrentUserService currentUser)
    {
        _requests = requests;
        _currentUser = currentUser;
    }

    public async Task<SubmissionResult> SubmitAsync(LeaveRequestSubmission s, SubmissionAttachment? attachment, CancellationToken ct = default)
    {
        if (_currentUser.UserId != s.UserId) throw new ForbiddenException("Không thể gửi đơn thay người khác.");

        var form = new Req.CreateLeaveRequestForm
        {
            LeaveTypeId = s.LeaveTypeId,
            Reason = s.Reason,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            StartAt = s.StartAt,
            EndAt = s.EndAt,
            SourceScheduleId = s.SourceScheduleId,
            TargetScheduleId = s.TargetScheduleId,
            TargetUserId = s.TargetUserId,
            AttendanceLogId = s.AttendanceLogId
        };
        var file = attachment is null ? null : new Req.RequestAttachment(attachment.Content, attachment.FileName, attachment.ContentType, attachment.Length);
        var created = await _requests.CreateAsync(form, file, ct);
        return new SubmissionResult(created.Id);
    }
}
