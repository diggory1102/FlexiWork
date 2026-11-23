using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;

namespace FW.UnitTests.Assistant;

public sealed record DemoUser(int Id, string Code, string FullName, int? BranchId, string[] Roles);

/// <summary>
/// Kho dữ liệu nhân sự trong bộ nhớ, cài đặt các cổng <see cref="IHrDataService"/> và <see cref="ILeaveRequestSubmitter"/>.
/// Dùng để chạy thử / kiểm thử khi chưa gắn vào FW.Backend. Thread-safe ở mức đủ cho demo.
/// </summary>
public sealed class InMemoryHrData : IHrDataService, ILeaveRequestSubmitter
{
    private readonly object _gate = new();
    private int _nextRequestId = 1000;

    public List<DemoUser> Users { get; } = new();
    public List<LeaveTypeDto> LeaveTypes { get; } = new();
    public List<(int UserId, ScheduledShiftDto Shift)> Schedules { get; } = new();
    public List<(int UserId, LeaveRequestDto Request)> Requests { get; } = new();
    public Dictionary<(int UserId, int Year), LeaveBalanceDto> Balances { get; } = new();
    public Dictionary<(int UserId, int Year, int Month), TimesheetSummaryDto> Timesheets { get; } = new();
    public List<LeaveRequestSubmission> Submitted { get; } = new();
    public List<SubmissionAttachment?> Attachments { get; } = new();
    public List<(int UserId, AttendanceLogDto Log)> AttendanceLogs { get; } = new();

    public Task<IReadOnlyList<AttendanceLogDto>> GetMyAttendanceLogsAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AttendanceLogDto>>(AttendanceLogs
                .Where(l => l.UserId == userId && l.Log.Date >= from && l.Log.Date <= to)
                .Select(l => l.Log).OrderBy(l => l.CheckinTime).ToList());
        }
    }

    public Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LeaveTypeDto>>(LeaveTypes.ToList());

    public Task<IReadOnlyList<ScheduledShiftDto>> GetMyScheduleAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ScheduledShiftDto>>(Schedules
                .Where(s => s.UserId == userId && s.Shift.Date >= from && s.Shift.Date <= to && s.Shift.Status != "cancelled")
                .Select(s => s.Shift)
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                .ToList());
        }
    }

    public Task<TimesheetSummaryDto> GetMyTimesheetAsync(int userId, int year, int month, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var found = Timesheets.TryGetValue((userId, year, month), out var t)
                ? t
                : new TimesheetSummaryDto(year, month, 26m, 0m, 0, 0, 0m, 0m, 0m, "draft");
            return Task.FromResult(found);
        }
    }

    public Task<LeaveBalanceDto?> GetMyLeaveBalanceAsync(int userId, int year, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!Balances.TryGetValue((userId, year), out var b)) return Task.FromResult<LeaveBalanceDto?>(null);

            var consuming = LeaveTypes.Where(t => t.ConsumesBalance).Select(t => t.Id).ToHashSet();
            var pending = Requests
                .Where(r => r.UserId == userId && r.Request.Status == "pending" && consuming.Contains(r.Request.LeaveTypeId) && r.Request.StartDate.Year == year)
                .Sum(r => r.Request.DurationDays);
            return Task.FromResult<LeaveBalanceDto?>(b with { PendingDays = pending });
        }
    }

    public Task<IReadOnlyList<LeaveRequestDto>> GetMyRequestsAsync(int userId, string? status, int limit, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<LeaveRequestDto>>(Requests
                .Where(r => r.UserId == userId && (status is null || r.Request.Status == status))
                .Select(r => r.Request)
                .OrderByDescending(r => r.CreatedAt)
                .Take(limit)
                .ToList());
        }
    }

    public Task<IReadOnlyList<ColleagueDto>> FindColleaguesAsync(int requesterId, string name, CancellationToken ct = default)
    {
        var me = Users.FirstOrDefault(u => u.Id == requesterId);
        var needle = RelativeDateResolver.Normalize(name);
        if (me is null || needle.Length == 0) return Task.FromResult<IReadOnlyList<ColleagueDto>>(Array.Empty<ColleagueDto>());

        var list = Users
            .Where(u => u.Id != requesterId && u.BranchId == me.BranchId && u.Roles.Contains("ROLE_EMPLOYEE"))
            .Where(u => RelativeDateResolver.Normalize(u.FullName).Contains(needle))
            .Select(u => new ColleagueDto(u.Id, u.FullName, u.Code))
            .ToList();
        return Task.FromResult<IReadOnlyList<ColleagueDto>>(list);
    }

    public Task<IReadOnlyList<ScheduledShiftDto>> GetColleagueShiftsForSwapAsync(
        int requesterId, int colleagueId, DateOnly date, CancellationToken ct = default)
    {
        var me = Users.FirstOrDefault(u => u.Id == requesterId);
        var other = Users.FirstOrDefault(u => u.Id == colleagueId);
        if (me is null || other is null || me.BranchId != other.BranchId)
            return Task.FromResult<IReadOnlyList<ScheduledShiftDto>>(Array.Empty<ScheduledShiftDto>());

        return GetMyScheduleAsync(colleagueId, date, date, ct);
    }

    public Task<IReadOnlyList<PendingRequestDto>> GetPendingRequestsAsync(int? branchId, int limit, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = Requests
                .Where(r => r.Request.Status == "pending")
                .Select(r => (r, u: Users.FirstOrDefault(u => u.Id == r.UserId)))
                .Where(x => x.u is not null && (branchId is null || x.u.BranchId == branchId))
                .OrderBy(x => x.r.Request.CreatedAt)
                .Take(limit)
                .Select(x => new PendingRequestDto(
                    x.r.Request.Id, x.u!.Id, x.u.FullName, x.r.Request.LeaveTypeName,
                    x.r.Request.StartDate, x.r.Request.EndDate, x.r.Request.DurationDays, x.r.Request.Reason, x.r.Request.CreatedAt))
                .ToList();
            return Task.FromResult<IReadOnlyList<PendingRequestDto>>(list);
        }
    }

    public Task<SubmissionResult> SubmitAsync(LeaveRequestSubmission s, SubmissionAttachment? attachment, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var type = LeaveTypes.First(t => t.Id == s.LeaveTypeId);
            var id = _nextRequestId++;
            Submitted.Add(s);
            Attachments.Add(attachment);
            Requests.Add((s.UserId, new LeaveRequestDto(
                id, type.Id, type.Name, type.Category, s.StartDate, s.EndDate, s.DurationDays, s.Reason,
                "pending", DateTime.UtcNow.AddHours(7), null)));
            return Task.FromResult(new SubmissionResult(id));
        }
    }
}
