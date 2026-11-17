using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Application.Features.Requests;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.Application.Features.Reports;

public interface IDashboardService
{
    /// <summary>Quân số và chấm công theo chi nhánh trong ngày (UC 9.1), kèm xu hướng 7 ngày gần nhất tới ngày xem.</summary>
    Task<DashboardDto> GetAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IViolationReportService
{
    /// <summary>Báo cáo đi muộn / về sớm / vắng mặt (UC 9.2): tổng hợp, theo chi nhánh, theo nhân viên.</summary>
    Task<ViolationReportDto> GetReportAsync(ViolationReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Từng lượt vi phạm của một nhân viên trong khoảng ngày.</summary>
    Task<EmployeeViolationDetailDto> GetEmployeeAsync(int userId, EmployeeViolationFilter filter, CancellationToken cancellationToken = default);
}

public interface ILeaveUsageReportService
{
    /// <summary>Báo cáo sử dụng quỹ ngày phép năm (UC 9.3).</summary>
    Task<LeaveUsageReportDto> GetAsync(LeaveUsageFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>Phạm vi chi nhánh của dashboard và báo cáo: Quản lý chỉ chi nhánh mình (chọn chi nhánh khác bị từ chối); Admin / HR chọn tùy ý.</summary>
internal static class ReportScope
{
    public static int? ResolveBranch(ICurrentUserService user, int? requested)
    {
        var scope = user.GetBranchScope(); // null = toàn doanh nghiệp; cũng chặn người không có quyền quản lý
        var branch = requested ?? scope;
        if (branch.HasValue) user.EnsureCanAccessBranch(branch);
        return branch;
    }

    public static ShiftDayFacts ToFacts(ShiftDayRow r) => new(r.StartAt, r.EndAt, r.OnLeave, r.LogStatus, r.CheckoutTime, r.LateMinutes, r.EarlyMinutes,
        r.LateGraceMinutes, r.EarlyLeaveGraceMinutes, r.Exempted);
}

// =============================================================================================== Dashboard

/// <summary>
/// Dashboard tổng quan (Task 5.6, UC 9.1). Tính tại thời điểm gọi từ lịch đã công bố và lượt chấm công nên luôn là số liệu hiện tại —
/// giao diện gọi lại định kỳ để có cảm giác thời gian thực. Phân loại ca theo <see cref="AttendanceReportRules"/>.
/// </summary>
public class DashboardService : IDashboardService
{
    public const int TrendDays = 7;

    private readonly IReportRepository _reports;
    private readonly IBranchRepository _branches;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public DashboardService(IReportRepository reports, IBranchRepository branches, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _reports = reports;
        _branches = branches;
        _currentUser = currentUser;
        _clock = clock;
    }

    private sealed record Classified(ShiftDayRow Row, ShiftViolations Result);

    public async Task<DashboardDto> GetAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var branchId = ReportScope.ResolveBranch(_currentUser, filter.BranchId);
        var now = _clock.Now;
        var date = (filter.Date ?? _clock.Today).ToDateTime(TimeOnly.MinValue);

        var rows = await _reports.GetShiftDaysAsync(date.AddDays(1 - TrendDays), date, branchId, userId: null, cancellationToken);
        var pending = await _reports.GetPendingCountsAsync(branchId, cancellationToken);
        var branches = (await _branches.GetAllAsync(cancellationToken)).Where(b => !branchId.HasValue || b.Id == branchId).ToList();

        var none = Array.Empty<ViolationRule>();
        var classified = rows.Select(r => new Classified(r, AttendanceReportRules.Evaluate(ReportScope.ToFacts(r), none, now))).ToList();
        var today = classified.Where(c => c.Row.WorkDate.Date == date).ToList();

        var branchCards = new List<BranchHeadcountDto>();
        foreach (var branch in branches.Where(b => b.Status == BranchStatus.Active || today.Any(c => c.Row.BranchId == b.Id)).OrderBy(b => b.Name))
        {
            var counts = pending.FirstOrDefault(p => p.BranchId == branch.Id);
            branchCards.Add(new BranchHeadcountDto(branch.Id, branch.Name,
                Headcount(today.Where(c => c.Row.BranchId == branch.Id), counts?.Logs ?? 0, counts?.Requests ?? 0)));
        }

        var totals = Headcount(today, pending.Sum(p => p.Logs), pending.Sum(p => p.Requests));
        var trend = Enumerable.Range(0, TrendDays).Select(i => date.AddDays(i + 1 - TrendDays)).Select(day =>
        {
            var d = classified.Where(c => c.Row.WorkDate.Date == day).ToList();
            var attended = d.Count(c => IsAttended(c.Result.State));
            var late = d.Count(c => c.Result.IsLate);
            return new DashboardTrendDto(day, d.Count, attended, late, d.Count(c => c.Result.IsAbsent), AttendanceReportRules.Rate(attended - late, attended));
        }).ToList();

        return new DashboardDto(now, date, totals, branchCards, trend);
    }

    private static bool IsAttended(ShiftDayState state) => state is ShiftDayState.Working or ShiftDayState.Completed;

    private static HeadcountDto Headcount(IEnumerable<Classified> items, int pendingLogs, int pendingRequests)
    {
        var list = items.ToList();
        var attended = list.Where(c => IsAttended(c.Result.State)).ToList();
        var late = list.Count(c => c.Result.IsLate);
        int Shifts(ShiftDayState state) => list.Count(c => c.Result.State == state);
        int Employees(IEnumerable<Classified> source) => source.Select(c => c.Row.UserId).Distinct().Count();

        return new HeadcountDto(
            ScheduledEmployees: Employees(list),
            ScheduledShifts: list.Count,
            CheckedInEmployees: Employees(attended),
            WorkingEmployees: Employees(list.Where(c => c.Result.State == ShiftDayState.Working)),
            WaitingCheckInShifts: Shifts(ShiftDayState.WaitingCheckIn),
            UpcomingShifts: Shifts(ShiftDayState.Upcoming),
            CompletedShifts: Shifts(ShiftDayState.Completed),
            AbsentShifts: Shifts(ShiftDayState.Absent),
            OnLeaveEmployees: Employees(list.Where(c => c.Result.State == ShiftDayState.OnLeave)),
            LateShifts: late,
            OnTimeRate: AttendanceReportRules.Rate(attended.Count - late, attended.Count),
            PendingAttendance: pendingLogs,
            PendingRequests: pendingRequests);
    }
}

// ================================================================================================ Báo cáo vi phạm

/// <summary>
/// Báo cáo đi muộn / về sớm / vắng mặt (Task 5.6, UC 9.2). Đánh giá từng ca bằng <see cref="AttendanceReportRules"/> với quy tắc vi phạm hiện hành
/// (đơn giải trình đã duyệt thì miễn). Tối đa <see cref="MaxRangeDays"/> ngày mỗi lần xem.
/// </summary>
public class ViolationReportService : IViolationReportService
{
    public const int MaxRangeDays = 93;

    private readonly IReportRepository _reports;
    private readonly IAttendancePolicyService _policy;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ViolationReportService(IReportRepository reports, IAttendancePolicyService policy, IUserRepository users, ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _reports = reports;
        _policy = policy;
        _users = users;
        _currentUser = currentUser;
        _clock = clock;
    }

    private sealed record Evaluated(ShiftDayRow Row, ShiftViolations Result);

    public async Task<ViolationReportDto> GetReportAsync(ViolationReportFilter filter, CancellationToken cancellationToken = default)
    {
        var branchId = ReportScope.ResolveBranch(_currentUser, filter.BranchId);
        var (from, to) = ResolveRange(filter.FromDate, filter.ToDate);
        var items = await LoadAsync(from, to, branchId, null, cancellationToken);

        var branches = items.GroupBy(e => e.Row.BranchId).OrderBy(g => g.First().Row.BranchName)
            .Select(g => new BranchViolationDto(g.Key, g.First().Row.BranchName ?? $"Chi nhánh #{g.Key}", Summarize(g))).ToList();

        var search = filter.Search?.Trim();
        var employees = items.GroupBy(e => e.Row.UserId)
            .Select(g => new { First = g.First().Row, Summary = Summarize(g) })
            .Where(x => x.Summary.ViolatingShifts > 0)
            .Where(x => string.IsNullOrEmpty(search) || x.First.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.First.EmployeeCode.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Summary.ViolatingShifts).ThenByDescending(x => x.Summary.PenaltyAmount).ThenBy(x => x.First.EmployeeCode)
            .Select(x => ToEmployeeDto(x.First, x.Summary)).ToList();

        var pageIndex = Math.Max(1, filter.PageIndex);
        var pageSize = filter.NormalizedPageSize;
        var page = PaginatedResult<EmployeeViolationDto>.Create(employees.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(), employees.Count, pageIndex, pageSize);

        return new ViolationReportDto(from, to, Summarize(items), branches, page);
    }

    public async Task<EmployeeViolationDetailDto> GetEmployeeAsync(int userId, EmployeeViolationFilter filter, CancellationToken cancellationToken = default)
    {
        _ = _currentUser.GetBranchScope(); // chỉ Quản lý / HR / Admin
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);
        if (!_currentUser.CanAccessBranch(user.BranchId)) throw new ForbiddenException("Nhân viên này không thuộc chi nhánh bạn quản lý.");

        var (from, to) = ResolveRange(filter.FromDate, filter.ToDate);
        var items = await LoadAsync(from, to, null, userId, cancellationToken);
        var violations = new List<ViolationItemDto>();
        foreach (var e in items.Where(i => i.Result.HasViolation))
        {
            var (r, v) = (e.Row, e.Result);
            ViolationItemDto Item(string type, int minutes, decimal penalty, decimal deduct) => new(r.WorkDate.Date, r.ShiftName, r.StartTime.ToString(@"hh\:mm"),
                r.EndAt.ToString("HH:mm"), type, minutes, penalty, deduct);

            if (v.IsLate) violations.Add(Item(ViolationType.Late, v.Late.Minutes, v.Late.PenaltyAmount, v.Late.DeductWorkUnits));
            if (v.IsEarly) violations.Add(Item(ViolationType.EarlyLeave, v.Early.Minutes, v.Early.PenaltyAmount, v.Early.DeductWorkUnits));
            if (v.IsAbsent) violations.Add(Item("absent", 0, 0, 0));
        }

        var first = items.FirstOrDefault()?.Row;
        var summary = Summarize(items);
        var employee = first is null
            ? new EmployeeViolationDto(user.Id, user.EmployeeCode, user.FullName, user.BranchId ?? 0, null, summary)
            : ToEmployeeDto(first, summary);
        return new EmployeeViolationDetailDto(employee, from, to, violations.OrderByDescending(v => v.WorkDate).ThenBy(v => v.StartTime).ToList());
    }

    // ------------------------------------------------------------------

    private async Task<List<Evaluated>> LoadAsync(DateTime from, DateTime to, int? branchId, int? userId, CancellationToken cancellationToken)
    {
        var rules = await _policy.GetRulesAsync(cancellationToken);
        var now = _clock.Now;
        var rows = await _reports.GetShiftDaysAsync(from, to, branchId, userId, cancellationToken);
        return rows.Select(r => new Evaluated(r, AttendanceReportRules.Evaluate(ReportScope.ToFacts(r), rules, now))).ToList();
    }

    private static ViolationSummaryDto Summarize(IEnumerable<Evaluated> source)
    {
        var list = source.ToList();
        var attended = list.Count(e => e.Result.State is ShiftDayState.Working or ShiftDayState.Completed);
        var absent = list.Count(e => e.Result.IsAbsent);
        var evaluated = attended + absent;
        var violating = list.Count(e => e.Result.HasViolation);

        return new ViolationSummaryDto(
            ScheduledShifts: list.Count,
            EvaluatedShifts: evaluated,
            AttendedShifts: attended,
            OnLeaveShifts: list.Count(e => e.Result.State == ShiftDayState.OnLeave),
            LateCount: list.Count(e => e.Result.IsLate),
            LateMinutes: list.Where(e => e.Result.IsLate).Sum(e => e.Result.Late.Minutes),
            EarlyCount: list.Count(e => e.Result.IsEarly),
            EarlyMinutes: list.Where(e => e.Result.IsEarly).Sum(e => e.Result.Early.Minutes),
            AbsentCount: absent,
            ViolatingShifts: violating,
            ViolationRate: AttendanceReportRules.Rate(violating, evaluated),
            PenaltyAmount: list.Sum(e => e.Result.Penalty),
            DeductedWorkDays: list.Sum(e => e.Result.DeductWorkUnits));
    }

    private static EmployeeViolationDto ToEmployeeDto(ShiftDayRow row, ViolationSummaryDto summary) =>
        new(row.UserId, row.EmployeeCode, row.FullName, row.BranchId, row.BranchName, summary);

    private (DateTime From, DateTime To) ResolveRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = _clock.Today;
        var to = toDate ?? today;
        var from = fromDate ?? new DateOnly(to.Year, to.Month, 1);
        if (to < from) throw new BadRequestException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays) throw new BadRequestException($"Mỗi lần xem báo cáo tối đa {MaxRangeDays} ngày.");
        return (from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue));
    }
}

// =============================================================================================== Quỹ phép

/// <summary>
/// Báo cáo sử dụng quỹ ngày phép (Task 5.6, UC 9.3): tổng quỹ / đã dùng / còn lại của nhân viên đang hoạt động (chưa có quỹ riêng thì dùng mức mặc định),
/// theo chi nhánh, theo loại nghỉ và theo tháng. Quản lý chỉ xem chi nhánh mình.
/// </summary>
public class LeaveUsageReportService : ILeaveUsageReportService
{
    /// <summary>Còn từ mức này trở xuống (và quỹ có phép) thì cảnh báo "còn ít phép".</summary>
    public const decimal LowBalanceDays = 2m;

    public const int TopCount = 10;

    private readonly IReportRepository _reports;
    private readonly ISystemSettingRepository _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public LeaveUsageReportService(IReportRepository reports, ISystemSettingRepository settings, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _reports = reports;
        _settings = settings;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<LeaveUsageReportDto> GetAsync(LeaveUsageFilter filter, CancellationToken cancellationToken = default)
    {
        var branchId = ReportScope.ResolveBranch(_currentUser, filter.BranchId);
        var year = filter.Year ?? _clock.Today.Year;
        if (year is < 2000 or > 2100) throw new BadRequestException("Năm không hợp lệ.");

        var defaultDays = await AnnualLeave.DefaultDaysAsync(_settings, cancellationToken);
        var rows = await _reports.GetLeaveBalancesAsync(year, branchId, defaultDays, cancellationToken);
        var types = await _reports.GetLeaveUsageByTypeAsync(year, branchId, cancellationToken);
        var months = (await _reports.GetLeaveUsageByMonthAsync(year, branchId, cancellationToken)).ToDictionary(m => m.Month, m => m.Days);

        var total = rows.Sum(r => r.TotalDays);
        var used = rows.Sum(r => r.UsedDays);
        var summary = new LeaveUsageSummaryDto(
            Employees: rows.Count,
            TotalDays: total,
            UsedDays: used,
            RemainingDays: rows.Sum(r => r.RemainingDays),
            UsageRate: Rate(used, total),
            PendingDays: types.Where(t => t.DeductsBalance).Sum(t => t.PendingDays),
            WithoutBalance: rows.Count(r => r.IsDefault),
            LowBalanceEmployees: rows.Count(IsLow));

        var branches = rows.GroupBy(r => r.BranchId).OrderBy(g => g.First().BranchName)
            .Select(g => new BranchLeaveUsageDto(g.Key, g.First().BranchName ?? "Chưa gán chi nhánh", g.Count(), g.Sum(r => r.TotalDays), g.Sum(r => r.UsedDays),
                g.Sum(r => r.RemainingDays), Rate(g.Sum(r => r.UsedDays), g.Sum(r => r.TotalDays)))).ToList();

        return new LeaveUsageReportDto(
            year, defaultDays, LowBalanceDays, summary, branches,
            types.Select(t => new LeaveTypeUsageDto(t.LeaveTypeId, t.Name, t.IsPaid, t.DeductsBalance, t.ApprovedRequests, t.ApprovedDays, t.PendingDays)).ToList(),
            Enumerable.Range(1, 12).Select(m => new MonthUsageDto(m, months.GetValueOrDefault(m))).ToList(),
            rows.Where(r => r.UsedDays > 0).OrderByDescending(r => r.UsedDays).ThenBy(r => r.EmployeeCode).Take(TopCount).Select(ToDto).ToList(),
            rows.Where(IsLow).OrderBy(r => r.RemainingDays).ThenBy(r => r.EmployeeCode).Take(TopCount).Select(ToDto).ToList());
    }

    private static bool IsLow(LeaveBalanceReportRow r) => r.TotalDays > 0 && r.RemainingDays <= LowBalanceDays;

    private static decimal? Rate(decimal part, decimal whole) => whole <= 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

    private static LeaveUsageEmployeeDto ToDto(LeaveBalanceReportRow r) =>
        new(r.UserId, r.EmployeeCode, r.FullName, r.BranchName, r.TotalDays, r.UsedDays, r.RemainingDays, r.IsDefault);
}
