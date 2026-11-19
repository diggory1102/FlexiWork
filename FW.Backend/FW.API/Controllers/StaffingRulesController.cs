using FW.Application.Features.Scheduling;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Ngoại lệ định biên ca theo chi nhánh / ngày / thứ trong tuần (Task 6.6, bước 2). Admin, HR (mọi chi nhánh), Quản lý (chi nhánh của mình).
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
[Route("api/staffing-rules")]
public class StaffingRulesController : ApiControllerBase
{
    private readonly IShiftStaffingService _staffing;

    public StaffingRulesController(IShiftStaffingService staffing)
    {
        _staffing = staffing;
    }

    /// <summary>Mọi ngoại lệ theo thứ trong tuần và các ngoại lệ theo ngày từ hôm nay trở đi của chi nhánh.</summary>
    [HttpGet]
    public async Task<IActionResult> GetRules([FromQuery] int? branchId, CancellationToken cancellationToken) =>
        Success(await _staffing.GetRulesAsync(branchId, cancellationToken));

    /// <summary>Định biên hiệu lực (đã áp ngoại lệ) của các ca trong khoảng ngày — dùng cho hàng "Định biên" của lưới xếp lịch.</summary>
    [HttpGet("effective")]
    public async Task<IActionResult> GetEffective([FromQuery] int? branchId, [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken) =>
        Success(await _staffing.GetEffectiveAsync(branchId, fromDate, toDate, cancellationToken));

    /// <summary>Đặt ngoại lệ cho một (chi nhánh, ca, ngày / thứ); đã có thì thay bằng giá trị mới.</summary>
    [HttpPut]
    public async Task<IActionResult> Save([FromBody] StaffingRuleRequest request, CancellationToken cancellationToken) =>
        Success(await _staffing.SaveAsync(request, cancellationToken), "Đã lưu ngoại lệ định biên.");

    /// <summary>Bỏ ngoại lệ — ô đó quay về định biên mặc định của ca.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _staffing.DeleteAsync(id, cancellationToken);
        return Success("Đã bỏ ngoại lệ định biên.");
    }
}
