using FW.Application.Common.Interfaces;
using FW.Application.Common.Security;
using FW.Application.Features.Requests;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Loại đơn từ (UC 1.4). Xem: mọi người dùng (màn hình tạo đơn chỉ thấy loại đang nhận đơn). Thêm/sửa/xóa: Admin, HR.
/// </summary>
[Authorize]
[Route("api/leave-types")]
public class LeaveTypesController : ApiControllerBase
{
    private readonly ILeaveTypeService _leaveTypeService;
    private readonly ICurrentUserService _currentUser;

    public LeaveTypesController(ILeaveTypeService leaveTypeService, ICurrentUserService currentUser)
    {
        _leaveTypeService = leaveTypeService;
        _currentUser = currentUser;
    }

    /// <param name="includeInactive">Admin/HR: gồm cả loại đã ngừng nhận đơn (màn hình cấu hình).</param>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Success(await _leaveTypeService.GetListAsync(!(includeInactive && _currentUser.HasCompanyWideAccess()), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _leaveTypeService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Create([FromBody] LeaveTypeRequest request, CancellationToken cancellationToken) =>
        Created(await _leaveTypeService.CreateAsync(request, cancellationToken), "Tạo loại đơn thành công.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Update(int id, [FromBody] LeaveTypeRequest request, CancellationToken cancellationToken) =>
        Success(await _leaveTypeService.UpdateAsync(id, request, cancellationToken), "Cập nhật loại đơn thành công.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _leaveTypeService.DeleteAsync(id, cancellationToken);
        return Success("Xóa loại đơn thành công.");
    }
}
