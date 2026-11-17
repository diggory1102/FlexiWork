using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Reports;
using FW.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Exports;

/// <summary>Tệp xuất ra cho người dùng tải về.</summary>
public sealed record ExportFile(string FileName, string ContentType, byte[] Content)
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public class ExportTimesheetsRequest
{
    public int? Year { get; set; }
    public int? Month { get; set; }

    /// <summary>Admin/HR: bỏ trống = mọi chi nhánh; Quản lý luôn là chi nhánh của mình.</summary>
    public int? BranchId { get; set; }
}

/// <summary>Dữ liệu một bảng công tháng để ghi ra Excel.</summary>
public sealed record TimesheetWorkbook(int Year, int Month, string ScopeLabel, DateTime ExportedAt, IReadOnlyList<TimesheetRow> Rows);

/// <summary>Dữ liệu một bảng lương (kỳ lương + phiếu lương + từng dòng) để ghi ra Excel.</summary>
public sealed record PayrollWorkbook(
    PayrollPeriodRow Period,
    DateTime ExportedAt,
    IReadOnlyList<PayslipRow> Payslips,
    IReadOnlyDictionary<long, IReadOnlyList<PayslipItem>> Items);

/// <summary>Dựng tệp Excel (.xlsx) — cài đặt ở Infrastructure (ClosedXML).</summary>
public interface IExcelExporter
{
    byte[] BuildTimesheets(TimesheetWorkbook data);

    byte[] BuildPayroll(PayrollWorkbook data);
}

public interface IExportService
{
    /// <summary>Xuất bảng công tháng ra Excel (Quản lý: chi nhánh mình; Admin / HR: tất cả hoặc một chi nhánh).</summary>
    Task<ExportFile> ExportTimesheetsAsync(ExportTimesheetsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xuất bảng lương của một kỳ ra Excel (Admin / HR).</summary>
    Task<ExportFile> ExportPayrollAsync(int periodId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xuất bảng công & bảng lương ra Excel cho kế toán (Task 5.7, UC 8.4). Phạm vi theo vai trò như màn hình xem: Quản lý chỉ chi nhánh mình,
/// bảng lương chỉ Admin / HR (giới hạn quyền ở Controller). Bảng lương của kỳ chưa chốt vẫn xuất được nhưng được ghi rõ là bản nháp.
/// </summary>
public class ExportService : IExportService
{
    private const int PageSize = 100;

    private readonly ITimesheetRepository _timesheets;
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayslipRepository _payslips;
    private readonly IBranchRepository _branches;
    private readonly IExcelExporter _excel;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        ITimesheetRepository timesheets,
        IPayrollPeriodRepository periods,
        IPayslipRepository payslips,
        IBranchRepository branches,
        IExcelExporter excel,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<ExportService> logger)
    {
        _timesheets = timesheets;
        _periods = periods;
        _payslips = payslips;
        _branches = branches;
        _excel = excel;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ExportFile> ExportTimesheetsAsync(ExportTimesheetsRequest request, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var year = request.Year ?? today.Year;
        var month = request.Month ?? today.Month;
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new BadRequestException("Tháng / năm không hợp lệ.");

        var branchId = ReportScope.ResolveBranch(_currentUser, request.BranchId);
        var rows = new List<TimesheetRow>();
        for (var pageIndex = 1; ; pageIndex++)
        {
            var page = await _timesheets.GetDetailsPagedAsync(new TimesheetQuery
            {
                Year = year, Month = month, BranchId = branchId, PageIndex = pageIndex, PageSize = PageSize
            }, cancellationToken);
            rows.AddRange(page.Items);
            if (!page.HasNextPage) break;
        }

        var scope = branchId is { } id
            ? (await _branches.GetByIdAsync(id, cancellationToken))?.Name ?? $"Chi nhánh #{id}"
            : "Tất cả chi nhánh";
        var content = _excel.BuildTimesheets(new TimesheetWorkbook(year, month, scope, _clock.Now, rows));

        _logger.LogInformation("Người dùng {UserId} xuất bảng công {Month}/{Year} ({Scope}): {Count} dòng", _currentUser.UserId, month, year, scope, rows.Count);
        var suffix = branchId.HasValue ? $"-cn{branchId}" : string.Empty;
        return new ExportFile($"bang-cong-{year}-{month:00}{suffix}.xlsx", ExportFile.XlsxContentType, content);
    }

    public async Task<ExportFile> ExportPayrollAsync(int periodId, CancellationToken cancellationToken = default)
    {
        var period = await _periods.GetRowAsync(periodId, cancellationToken) ?? throw new NotFoundException("Kỳ lương", periodId);
        var payslips = await _payslips.GetRowsByPeriodAsync(periodId, onlyUnsent: false, ids: null, cancellationToken);
        var items = await _payslips.GetItemsByPayslipsAsync(payslips.Select(p => p.Id).ToList(), cancellationToken);
        var content = _excel.BuildPayroll(new PayrollWorkbook(period, _clock.Now, payslips, items));

        _logger.LogInformation("Người dùng {UserId} xuất bảng lương {Month}/{Year} ({Status}): {Count} phiếu", _currentUser.UserId, period.Month, period.Year,
            period.Status, payslips.Count);
        return new ExportFile($"bang-luong-{period.Year}-{period.Month:00}.xlsx", ExportFile.XlsxContentType, content);
    }
}
