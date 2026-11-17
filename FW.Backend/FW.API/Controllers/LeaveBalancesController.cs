using FW.Application.Features.Requests;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Quỹ phép năm (UC 5.5). Xem danh sách: Admin, HR (mọi chi nhánh), Quản lý (chi nhánh mình). Cộng / trừ quỹ: Admin, HR.
/// Quỹ phép của chính mình: GET /api/leave-balances/me (LeaveRequestsController).
/// </summary>
[Authorize]
[Route("api/leave-balances")]
public class LeaveBalancesController : ApiControllerBase
{
    private readonly ILeaveBalanceService _balanceService;

    public LeaveBalancesController(ILeaveBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>Quỹ phép theo nhân viên: lọc năm (mặc định năm nay), chi nhánh, tên / mã NV; phân trang.</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetBalances([FromQuery] LeaveBalanceFilter filter, CancellationToken cancellationToken) =>
        Success(await _balanceService.GetBalancesAsync(filter, cancellationToken));

    /// <summary>Cộng / trừ tổng quỹ phép năm: {userId, year, deltaDays, reason}.</summary>
    [HttpPost("adjust")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Adjust([FromBody] AdjustLeaveBalanceRequest request, CancellationToken cancellationToken)
    {
        var result = await _balanceService.AdjustAsync(request, cancellationToken);
        var remaining = result.RemainingDays.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
        return Success(result, $"Đã điều chỉnh quỹ phép năm {result.Year} của {result.FullName}: còn {remaining} ngày.");
    }

    /// <summary>Lịch sử điều chỉnh quỹ phép của nhân viên trong năm (chính chủ hoặc người quản lý).</summary>
    [HttpGet("{userId:int}/adjustments")]
    public async Task<IActionResult> GetAdjustments(int userId, [FromQuery] int? year, CancellationToken cancellationToken) =>
        Success(await _balanceService.GetAdjustmentsAsync(userId, year, cancellationToken));
}
