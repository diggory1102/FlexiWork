using FW.Application.Features.Payroll;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Bảng lương rút gọn (UC 8.2 – 8.4) — chỉ Admin, HR: tính từ bảng công đã khóa, chốt kỳ, gửi phiếu lương qua email.
/// Nhân viên xem phiếu lương của mình ở <c>/api/payslips</c>.
/// </summary>
[Authorize(Roles = RoleNames.AdminOrHr)]
[Route("api/payroll")]
public class PayrollController : ApiControllerBase
{
    private readonly IPayrollService _service;

    public PayrollController(IPayrollService service)
    {
        _service = service;
    }

    /// <summary>Các kỳ lương (mới nhất trước), lọc theo năm.</summary>
    [HttpGet("periods")]
    public async Task<IActionResult> GetPeriods([FromQuery] PayrollPeriodFilter filter, CancellationToken cancellationToken) =>
        Success(await _service.GetPeriodsAsync(filter, cancellationToken));

    /// <summary>Chi tiết kỳ lương: tổng hợp từng loại khoản và các điều kiện còn thiếu trước khi chốt.</summary>
    [HttpGet("periods/{id:int}")]
    public async Task<IActionResult> GetPeriod(int id, CancellationToken cancellationToken) =>
        Success(await _service.GetPeriodAsync(id, cancellationToken));

    /// <summary>Tính (lại) bảng lương nháp của tháng từ các bảng công đã khóa: {year, month}.</summary>
    [HttpPost("periods/calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculatePayrollRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CalculateAsync(request, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Chốt kỳ lương (tính lại lần cuối rồi khóa số liệu, không mở lại được).</summary>
    [HttpPost("periods/{id:int}/finalize")]
    public async Task<IActionResult> Finalize(int id, CancellationToken cancellationToken)
    {
        var result = await _service.FinalizeAsync(id, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Gửi phiếu lương qua email (kỳ đã chốt): {payslipIds?} — bỏ trống = mọi phiếu chưa gửi.</summary>
    [HttpPost("periods/{id:int}/send")]
    public async Task<IActionResult> Send(int id, [FromBody] SendPayslipsRequest? request, CancellationToken cancellationToken)
    {
        var result = await _service.SendAsync(id, request ?? new SendPayslipsRequest(), cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Phiếu lương của kỳ: branchId, status (draft / sent), search; phân trang.</summary>
    [HttpGet("periods/{id:int}/payslips")]
    public async Task<IActionResult> GetPayslips(int id, [FromQuery] PayslipFilter filter, CancellationToken cancellationToken) =>
        Success(await _service.GetPayslipsAsync(id, filter, cancellationToken));
}
