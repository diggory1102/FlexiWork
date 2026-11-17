using FW.Application.Features.Exports;
using FW.Application.Features.Timesheets;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Bảng công tháng (UC 8.2). Xem bảng công của mình: mọi người dùng. Danh sách / tính / xác nhận: Quản lý (chi nhánh mình), Admin, HR.
/// Khóa: Admin, HR. Mở lại: Quản lý mở bảng đã xác nhận của chi nhánh mình; HR / Admin mở cả bảng đã khóa.
/// </summary>
[Authorize]
[Route("api/timesheets")]
public class TimesheetsController : ApiControllerBase
{
    private readonly ITimesheetService _service;
    private readonly IExportService _export;

    public TimesheetsController(ITimesheetService service, IExportService export)
    {
        _service = service;
        _export = export;
    }

    /// <summary>Bảng công của nhân viên trong tháng: year, month (mặc định tháng này), branchId, status, search; phân trang.</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetTimesheets([FromQuery] TimesheetFilter filter, CancellationToken cancellationToken) =>
        Success(await _service.GetPagedAsync(filter, cancellationToken));

    /// <summary>Xuất bảng công tháng ra Excel (.xlsx): year, month (mặc định tháng này), branchId. Quản lý chỉ xuất chi nhánh mình.</summary>
    [HttpGet("export")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Export([FromQuery] ExportTimesheetsRequest request, CancellationToken cancellationToken)
    {
        var file = await _export.ExportTimesheetsAsync(request, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Đếm bảng công theo trạng thái của tháng.</summary>
    [HttpGet("summary")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetSummary([FromQuery] int year, [FromQuery] int month, [FromQuery] int? branchId, CancellationToken cancellationToken) =>
        Success(await _service.GetSummaryAsync(year, month, branchId, cancellationToken));

    /// <summary>Bảng công của tôi trong tháng (đã xác nhận → bản lưu; còn lại → tính trực tiếp, isPreview = true).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] MyTimesheetFilter filter, CancellationToken cancellationToken) =>
        Success(await _service.GetMineAsync(filter, cancellationToken));

    /// <summary>Chi tiết bảng công: số liệu, từng ca trong tháng, OT, cảnh báo số liệu nguồn đã đổi. Chính chủ hoặc người quản lý.</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id, CancellationToken cancellationToken) =>
        Success(await _service.GetDetailAsync(id, cancellationToken));

    /// <summary>Tính (lại) bảng công nháp: {year, month, branchId?, userIds?}.</summary>
    [HttpPost("calculate")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Calculate([FromBody] CalculateTimesheetsRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CalculateAsync(request, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Xác nhận bảng công nháp (chặn khi còn lượt chấm công chờ duyệt): {year, month, branchId?, userIds?}.</summary>
    [HttpPost("confirm")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Confirm([FromBody] CalculateTimesheetsRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ConfirmAsync(request, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Khóa bảng công đã xác nhận của tháng đã kết thúc: {year, month, branchId?, userIds?}.</summary>
    [HttpPost("lock")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Lock([FromBody] CalculateTimesheetsRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.LockAsync(request, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Mở lại bảng công về nháp: {note} (bắt buộc).</summary>
    [HttpPost("{id:long}/reopen")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Reopen(long id, [FromBody] ReopenTimesheetRequest request, CancellationToken cancellationToken) =>
        Success(await _service.ReopenAsync(id, request, cancellationToken), "Đã mở lại bảng công về nháp.");
}
