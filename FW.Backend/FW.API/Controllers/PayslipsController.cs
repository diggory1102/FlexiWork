using FW.Application.Features.Payroll;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Phiếu lương cá nhân (UC 8.5). Mọi người dùng xem phiếu của mình khi kỳ lương đã chốt; Admin / HR xem được mọi phiếu.
/// </summary>
[Authorize]
[Route("api/payslips")]
public class PayslipsController : ApiControllerBase
{
    private readonly IPayslipService _service;

    public PayslipsController(IPayslipService service)
    {
        _service = service;
    }

    /// <summary>Phiếu lương của tôi (các kỳ đã chốt, mới nhất trước): year, pageIndex, pageSize.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] MyPayslipFilter filter, CancellationToken cancellationToken) =>
        Success(await _service.GetMineAsync(filter, cancellationToken));

    /// <summary>Chi tiết phiếu lương kèm từng khoản.</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id, CancellationToken cancellationToken) =>
        Success(await _service.GetDetailAsync(id, cancellationToken));
}
