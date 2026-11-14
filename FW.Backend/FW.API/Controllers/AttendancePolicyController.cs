using FW.Application.Features.Scheduling;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Số công chuẩn tháng & quy tắc trừ công / phạt khi đi muộn, về sớm (UC 1.5).
/// Xem: Admin, HR, Quản lý. Sửa: Admin, HR.
/// </summary>
[Route("api/attendance-policy")]
[Authorize(Roles = RoleNames.Managers)]
public class AttendancePolicyController : ApiControllerBase
{
    private readonly IAttendancePolicyService _policyService;

    public AttendancePolicyController(IAttendancePolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Success(await _policyService.GetAsync(cancellationToken));

    /// <summary>Lưu số công chuẩn và thay toàn bộ bậc xử lý vi phạm.</summary>
    [HttpPut]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Update([FromBody] AttendancePolicyRequest request, CancellationToken cancellationToken) =>
        Success(await _policyService.UpdateAsync(request, cancellationToken), "Lưu quy tắc chấm công thành công.");
}
