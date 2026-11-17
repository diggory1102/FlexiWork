using System.Globalization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Features.Notifications;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Payroll;

public interface IPayrollService
{
    Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(PayrollPeriodFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết kỳ lương: tổng hợp và các điều kiện còn thiếu trước khi chốt.</summary>
    Task<PayrollPeriodDetailDto> GetPeriodAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tính (lại) bảng lương nháp của tháng từ các bảng công đã khóa.</summary>
    Task<CalculatePayrollResult> CalculateAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);

    /// <summary>Chốt kỳ lương: tính lại lần cuối rồi khóa số liệu (phiếu lương, bảng công → đã chốt).</summary>
    Task<FinalizePayrollResult> FinalizeAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Gửi phiếu lương qua email cho nhân viên (kỳ đã chốt).</summary>
    Task<SendPayslipsResult> SendAsync(int id, SendPayslipsRequest request, CancellationToken cancellationToken = default);

    Task<PaginatedResult<PayslipDto>> GetPayslipsAsync(int periodId, PayslipFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bảng lương rút gọn (Task 5.5, UC 8.2 – 8.4) — Admin/HR (giới hạn quyền ở Controller). Nguồn: bảng công <b>đã khóa</b> (Task 5.3) + cấu hình lương
/// (Task 5.4); công thức ở <see cref="PayslipCalculator"/>. Phiếu lương là ảnh chụp số liệu lúc tính (đổi cấu hình sau đó không làm đổi phiếu cũ).
/// <list type="bullet">
/// <item><b>Tính:</b> chỉ tính khi tháng chưa chốt, ghi đè toàn bộ phiếu nháp trong một transaction. Nhân viên chưa cấu hình lương bị bỏ qua và báo lý do.</item>
/// <item><b>Chốt:</b> tháng đã kết thúc, mọi bảng công đã khóa và đủ phiếu; tính lại lần cuối rồi chuyển kỳ và bảng công sang <c>finalized</c> (không mở lại).</item>
/// <item><b>Gửi:</b> chỉ kỳ đã chốt; lỗi gửi email từng người không ảnh hưởng người khác và có thể gửi lại. Nhân viên xem được phiếu trong ứng dụng ngay khi kỳ chốt.</item>
/// </list>
/// </summary>
public class PayrollService : IPayrollService
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayslipRepository _payslips;
    private readonly ITimesheetRepository _timesheets;
    private readonly IPayrollConfigRepository _employees;
    private readonly IAllowanceRepository _allowances;
    private readonly IUserAllowanceRepository _userAllowances;
    private readonly IPayrollConfigService _config;
    private readonly IEmailService _email;
    private readonly INotifier _notifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PayrollService> _logger;

    public PayrollService(
        IPayrollPeriodRepository periods,
        IPayslipRepository payslips,
        ITimesheetRepository timesheets,
        IPayrollConfigRepository employees,
        IAllowanceRepository allowances,
        IUserAllowanceRepository userAllowances,
        IPayrollConfigService config,
        IEmailService email,
        INotifier notifier,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<PayrollService> logger)
    {
        _periods = periods;
        _payslips = payslips;
        _timesheets = timesheets;
        _employees = employees;
        _allowances = allowances;
        _userAllowances = userAllowances;
        _config = config;
        _email = email;
        _notifier = notifier;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    private int Me => _currentUser.UserId ?? throw new UnauthorizedException();

    // ------------------------------------------------------------------ Truy vấn

    public async Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(PayrollPeriodFilter filter, CancellationToken cancellationToken = default) =>
        (await _periods.GetRowsAsync(filter.Year, cancellationToken)).Select(PayrollPeriodDto.From).ToList();

    public async Task<PayrollPeriodDetailDto> GetPeriodAsync(int id, CancellationToken cancellationToken = default)
    {
        var period = await LoadAsync(id, cancellationToken);
        var totals = await _payslips.GetTotalsAsync(id, cancellationToken);
        return new PayrollPeriodDetailDto(PayrollPeriodDto.From(period), PayslipTotalsDto.From(totals), await ReadinessAsync(period, cancellationToken));
    }

    public async Task<PaginatedResult<PayslipDto>> GetPayslipsAsync(int periodId, PayslipFilter filter, CancellationToken cancellationToken = default)
    {
        await LoadAsync(periodId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status is not (PayslipStatus.Draft or PayslipStatus.Sent))
        {
            throw new BadRequestException("Trạng thái không hợp lệ (draft, sent).");
        }

        var page = await _payslips.GetDetailsPagedAsync(new PayslipQuery
        {
            PeriodId = periodId,
            BranchId = filter.BranchId,
            Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status,
            Search = filter.Search,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        }, cancellationToken);
        return page.Map(PayslipDto.From);
    }

    // ------------------------------------------------------------------ Tính

    public async Task<CalculatePayrollResult> CalculateAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePeriod(request.Year, request.Month);
        var today = _clock.Today;
        if (request.Year > today.Year || (request.Year == today.Year && request.Month > today.Month))
        {
            throw new BadRequestException($"Tháng {request.Month:00}/{request.Year} chưa tới — chưa tính lương được.");
        }

        var periodId = await _periods.EnsureAsync(request.Year, request.Month, Me, cancellationToken);
        var period = await LoadAsync(periodId, cancellationToken);
        EnsureDraft(period);

        var plan = await BuildAsync(request.Year, request.Month, cancellationToken);
        await _unitOfWork.ExecuteInTransactionAsync(ct => SavePlanAsync(period.Id, plan, ct), cancellationToken);

        var message = plan.Payslips.Count == 0
            ? $"Tháng {request.Month:00}/{request.Year} chưa có bảng công nào đã khóa để tính lương."
            : $"Đã tính {plan.Payslips.Count} phiếu lương tháng {request.Month:00}/{request.Year}, tổng thực nhận {plan.TotalNet.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ.";
        if (plan.Unlocked > 0) message += $" {plan.Unlocked} bảng công chưa khóa nên chưa được tính.";
        if (plan.Skipped.Count > 0) message += $" {plan.Skipped.Count} nhân viên chưa cấu hình lương nên bị bỏ qua.";

        _logger.LogInformation("Người dùng {UserId} tính lương {Month}/{Year}: {Count} phiếu", Me, request.Month, request.Year, plan.Payslips.Count);
        var saved = await LoadAsync(period.Id, cancellationToken);
        return new CalculatePayrollResult(PayrollPeriodDto.From(saved), plan.Payslips.Count, plan.Unlocked, plan.Skipped, message);
    }

    // ------------------------------------------------------------------ Chốt

    public async Task<FinalizePayrollResult> FinalizeAsync(int id, CancellationToken cancellationToken = default)
    {
        var me = Me;
        var period = await LoadAsync(id, cancellationToken);
        EnsureDraft(period);
        if (!MonthEnded(period.Year, period.Month))
        {
            throw new BadRequestException($"Tháng {period.Month:00}/{period.Year} chưa kết thúc — chỉ chốt lương của tháng đã qua.");
        }

        // Tính lại lần cuối để phiếu luôn khớp bảng công và cấu hình lương mới nhất
        var plan = await BuildAsync(period.Year, period.Month, cancellationToken);
        var problems = new List<string>();
        if (plan.Unlocked > 0) problems.Add($"Còn {plan.Unlocked} bảng công chưa khóa — HR cần khóa (hoặc mở lại và xử lý) trước khi chốt lương.");
        foreach (var skipped in plan.Skipped) problems.Add($"{skipped.FullName} ({skipped.EmployeeCode}): {skipped.Reason}");
        if (plan.Payslips.Count == 0) problems.Add("Chưa có bảng công đã khóa nào để tính lương.");
        if (problems.Count > 0) throw new BadRequestException("Chưa chốt được kỳ lương.", problems);

        var now = _clock.Now;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await SavePlanAsync(period.Id, plan, ct);
            if (!await _periods.TryFinalizeAsync(period.Id, me, now, ct)) throw new BadRequestException("Kỳ lương vừa được người khác chốt.");
            await _timesheets.FinalizeLockedAsync(period.Year, period.Month, ct);
        }, cancellationToken);

        _logger.LogInformation("Người dùng {UserId} chốt kỳ lương {Month}/{Year}: {Count} phiếu, tổng {Net}", me, period.Month, period.Year, plan.Payslips.Count,
            plan.TotalNet);

        // Báo cho nhân viên sau khi commit (không bao giờ ném lỗi)
        await _notifier.NotifyAsync(plan.Payslips.Select(p => p.Payslip.UserId), NotificationType.Payslip, $"Phiếu lương tháng {period.Month:00}/{period.Year}",
            "Phiếu lương của bạn đã sẵn sàng — mở ứng dụng để xem chi tiết.", period.Id.ToString(), cancellationToken);

        var saved = await LoadAsync(period.Id, cancellationToken);
        return new FinalizePayrollResult(PayrollPeriodDto.From(saved), plan.Payslips.Count, plan.TotalNet,
            $"Đã chốt lương tháng {period.Month:00}/{period.Year}: {plan.Payslips.Count} phiếu lương.");
    }

    // ------------------------------------------------------------------ Gửi phiếu lương

    public async Task<SendPayslipsResult> SendAsync(int id, SendPayslipsRequest request, CancellationToken cancellationToken = default)
    {
        var period = await LoadAsync(id, cancellationToken);
        if (period.Status != PayrollPeriodStatus.Finalized) throw new BadRequestException("Chỉ gửi phiếu lương của kỳ đã chốt.");

        var ids = request.PayslipIds is { Count: > 0 } ? request.PayslipIds.Distinct().ToList() : null;
        var rows = await _payslips.GetRowsByPeriodAsync(id, onlyUnsent: ids is null, ids, cancellationToken);
        if (ids is not null && rows.Count != ids.Count) throw new BadRequestException("Có phiếu lương không thuộc kỳ này.");
        if (rows.Count == 0) return new SendPayslipsResult(0, 0, Array.Empty<PayrollSkippedDto>(), "Mọi phiếu lương của kỳ này đã được gửi.");

        var items = await _payslips.GetItemsByPayslipsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        var sent = new List<long>();
        var failures = new List<PayrollSkippedDto>();
        foreach (var row in rows)
        {
            try
            {
                await _email.SendAsync(row.Email, PayslipEmailTemplate.Subject(row.Month, row.Year),
                    PayslipEmailTemplate.Build(row, items.GetValueOrDefault(row.Id, Array.Empty<PayslipItem>())), cancellationToken);
                sent.Add(row.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gửi phiếu lương {PayslipId} tới {Email} thất bại", row.Id, row.Email);
                failures.Add(new PayrollSkippedDto(row.UserId, row.EmployeeCode, row.FullName, "Gửi email thất bại — thử gửi lại sau."));
            }
        }

        await _payslips.MarkSentAsync(sent, _clock.Now, cancellationToken);
        var message = $"Đã gửi {sent.Count} phiếu lương.";
        if (failures.Count > 0) message += $" {failures.Count} phiếu gửi thất bại, có thể gửi lại.";
        _logger.LogInformation("Người dùng {UserId} gửi phiếu lương kỳ {PeriodId}: {Sent} thành công, {Failed} lỗi", Me, id, sent.Count, failures.Count);
        return new SendPayslipsResult(sent.Count, failures.Count, failures, message);
    }

    // ------------------------------------------------------------------ Tính toán

    private sealed record PayrollPlan(
        IReadOnlyList<PayslipWithItems> Payslips,
        IReadOnlyDictionary<long, decimal> Salaries,
        IReadOnlyList<PayrollSkippedDto> Skipped,
        int Unlocked,
        decimal StandardWorkDays,
        decimal OvertimeMultiplier)
    {
        public decimal TotalNet => Payslips.Sum(p => p.Payslip.NetPay);
    }

    /// <summary>Tính phiếu lương cho mọi bảng công đã khóa của tháng (chưa ghi gì vào CSDL).</summary>
    private async Task<PayrollPlan> BuildAsync(int year, int month, CancellationToken cancellationToken)
    {
        var settings = await _config.GetSettingsAsync(cancellationToken);
        var sheets = await _timesheets.GetByMonthAsync(year, month, cancellationToken);
        var locked = sheets.Where(t => t.Status == TimesheetStatus.Locked).ToList();
        var unlocked = sheets.Count(t => t.Status is TimesheetStatus.Draft or TimesheetStatus.Confirmed);

        var userIds = locked.Select(t => t.UserId).Distinct().ToList();
        var employees = (await _employees.GetEmployeesByIdsAsync(userIds, cancellationToken)).ToDictionary(e => e.UserId);
        var catalog = (await _allowances.GetListAsync(activeOnly: true, cancellationToken)).ToList();
        var assigned = (await _userAllowances.GetByUsersAsync(userIds, cancellationToken)).ToLookup(a => a.UserId);

        var payslips = new List<PayslipWithItems>();
        var salaries = new Dictionary<long, decimal>();
        var skipped = new List<PayrollSkippedDto>();
        foreach (var sheet in locked.OrderBy(t => t.UserId))
        {
            if (!employees.TryGetValue(sheet.UserId, out var employee)) continue;

            var effectiveRate = PayrollRules.EffectiveHourlyRate(employee.HourlyRate, employee.PositionHourlyRate);
            if (!PayrollRules.IsConfigured(employee.EmploymentType, employee.BaseSalary, effectiveRate))
            {
                var reason = employee.EmploymentType == EmploymentType.PartTime
                    ? "Chưa có đơn giá giờ (chức vụ hoặc mức riêng) — cấu hình lương rồi tính lại."
                    : "Chưa có lương cơ bản — cấu hình lương rồi tính lại.";
                skipped.Add(new PayrollSkippedDto(employee.UserId, employee.EmployeeCode, employee.FullName, reason));
                continue;
            }

            var own = assigned[sheet.UserId].ToDictionary(a => a.AllowanceId);
            var allowances = catalog
                .Where(a => a.ApplyToAll || own.ContainsKey(a.Id))
                .Select(a => new PayslipAllowanceInput(a.Name, a.Basis, PayrollRules.AllowanceUnitAmount(a.Amount, own.GetValueOrDefault(a.Id)?.Amount)))
                .ToList();

            var result = PayslipCalculator.Calculate(new PayslipInput(employee.EmploymentType, employee.BaseSalary, employee.HourlyRate, employee.PositionHourlyRate,
                settings.StandardWorkDays, settings.OvertimeMultiplier, sheet.ActualWorkDays, sheet.ActualWorkHours, sheet.PaidLeaveDays, sheet.TotalOvertimeHours,
                sheet.PenaltyAmount, sheet.ViolationCount, sheet.TotalLateMinutes, sheet.TotalEarlyMinutes, sheet.NightShifts, sheet.SplitShiftDays, allowances));

            var payslip = new Payslip
            {
                UserId = sheet.UserId,
                BranchId = sheet.BranchId,
                TimesheetId = sheet.Id,
                EmploymentType = employee.EmploymentType,
                BaseSalary = employee.BaseSalary ?? 0,
                HourlyRate = result.HourlyRate,
                StandardWorkDays = settings.StandardWorkDays,
                WorkDays = sheet.ActualWorkDays,
                WorkHours = sheet.ActualWorkHours,
                OvertimeHours = sheet.TotalOvertimeHours,
                BasicPay = result.BasicPay,
                AllowanceTotal = result.AllowanceTotal,
                OvertimePay = result.OvertimePay,
                PenaltyTotal = result.PenaltyTotal,
                NetPay = result.NetPay,
                Status = PayslipStatus.Draft
            };
            var items = result.Lines.Select(l => new PayslipItem { Kind = l.Kind, Name = l.Name, Quantity = l.Quantity, UnitAmount = l.UnitAmount, Amount = l.Amount, Note = l.Note })
                .ToList();
            payslips.Add(new PayslipWithItems(payslip, items));
            salaries[sheet.Id] = result.NetPay;
        }

        return new PayrollPlan(payslips, salaries, skipped, unlocked, settings.StandardWorkDays, settings.OvertimeMultiplier);
    }

    private async Task SavePlanAsync(int periodId, PayrollPlan plan, CancellationToken cancellationToken)
    {
        await _payslips.ReplaceForPeriodAsync(periodId, plan.Payslips, cancellationToken);
        if (!await _periods.TryUpdateCalculationAsync(periodId, plan.StandardWorkDays, plan.OvertimeMultiplier, plan.Payslips.Count, plan.TotalNet, _clock.Now,
                cancellationToken))
        {
            throw new BadRequestException("Kỳ lương vừa được người khác chốt — tải lại để xem kết quả.");
        }
        await _timesheets.SetCalculatedSalariesAsync(plan.Salaries, cancellationToken);
    }

    private async Task<PayrollReadinessDto> ReadinessAsync(PayrollPeriodRow period, CancellationToken cancellationToken)
    {
        var counts = await _timesheets.CountByStatusAsync(period.Year, period.Month, null, cancellationToken);
        int Count(string status) => counts.GetValueOrDefault(status);

        var lockedCount = period.Status == PayrollPeriodStatus.Finalized ? Count(TimesheetStatus.Finalized) : Count(TimesheetStatus.Locked);
        var unlocked = Count(TimesheetStatus.Draft) + Count(TimesheetStatus.Confirmed);
        var missing = Math.Max(0, lockedCount - period.PayslipCount);
        var ended = MonthEnded(period.Year, period.Month);

        var problems = new List<string>();
        if (period.Status == PayrollPeriodStatus.Draft)
        {
            if (!ended) problems.Add($"Tháng {period.Month:00}/{period.Year} chưa kết thúc.");
            if (unlocked > 0) problems.Add($"Còn {unlocked} bảng công chưa khóa.");
            if (lockedCount == 0) problems.Add("Chưa có bảng công đã khóa nào.");
            if (missing > 0) problems.Add($"{missing} bảng công đã khóa chưa có phiếu lương — tính lương lại; nhân viên chưa cấu hình lương sẽ được báo cụ thể.");
        }
        var canFinalize = period.Status == PayrollPeriodStatus.Draft && problems.Count == 0;
        return new PayrollReadinessDto(ended, lockedCount, unlocked, missing, canFinalize, problems);
    }

    // ------------------------------------------------------------------

    private async Task<PayrollPeriodRow> LoadAsync(int id, CancellationToken cancellationToken) =>
        await _periods.GetRowAsync(id, cancellationToken) ?? throw new NotFoundException("Kỳ lương", id);

    private static void EnsureDraft(PayrollPeriodRow period)
    {
        if (period.Status != PayrollPeriodStatus.Draft) throw new BadRequestException($"Kỳ lương {period.Month:00}/{period.Year} đã chốt, không tính lại được.");
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new BadRequestException("Tháng / năm không hợp lệ.");
    }

    private bool MonthEnded(int year, int month) => new DateTime(year, month, 1).AddMonths(1) <= _clock.Now.Date;
}
