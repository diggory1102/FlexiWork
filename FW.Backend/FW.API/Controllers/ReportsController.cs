using FW.Application.Features.Reports;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Dashboard và báo cáo (UC 9.1 – 9.3) — Admin, HR (mọi chi nhánh) và Quản lý (chi nhánh mình; chọn chi nhánh khác bị từ chối).
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly IViolationReportService _violations;
    private readonly ILeaveUsageReportService _leaveUsage;

    public ReportsController(IDashboardService dashboard, IViolationReportService violations, ILeaveUsageReportService leaveUsage)
    {
        _dashboard = dashboard;
        _violations = violations;
        _leaveUsage = leaveUsage;
    }

    /// <summary>Quân số và chấm công theo chi nhánh trong ngày + xu hướng 7 ngày: branchId, date (mặc định hôm nay). Gọi lại định kỳ để cập nhật.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DashboardFilter filter, CancellationToken cancellationToken) =>
        Success(await _dashboard.GetAsync(filter, cancellationToken));

    /// <summary>Báo cáo đi muộn / về sớm / vắng mặt: fromDate, toDate (tối đa 93 ngày), branchId, search; phân trang theo nhân viên.</summary>
    [HttpGet("violations")]
    public async Task<IActionResult> GetViolations([FromQuery] ViolationReportFilter filter, CancellationToken cancellationToken) =>
        Success(await _violations.GetReportAsync(filter, cancellationToken));

    /// <summary>Từng lượt vi phạm của một nhân viên: fromDate, toDate.</summary>
    [HttpGet("violations/employees/{userId:int}")]
    public async Task<IActionResult> GetEmployeeViolations(int userId, [FromQuery] EmployeeViolationFilter filter, CancellationToken cancellationToken) =>
        Success(await _violations.GetEmployeeAsync(userId, filter, cancellationToken));

    /// <summary>Báo cáo sử dụng quỹ ngày phép năm: year (mặc định năm nay), branchId.</summary>
    [HttpGet("leave-usage")]
    public async Task<IActionResult> GetLeaveUsage([FromQuery] LeaveUsageFilter filter, CancellationToken cancellationToken) =>
        Success(await _leaveUsage.GetAsync(filter, cancellationToken));
}
