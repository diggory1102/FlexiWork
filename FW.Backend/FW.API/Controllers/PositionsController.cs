using FW.Application.Common.Models;
using FW.Application.Features.Organization;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Chức vụ & đơn giá công theo giờ. Xem: Admin, HR, Manager. Thêm/sửa/xóa: Admin, HR.
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
public class PositionsController : ApiControllerBase
{
    private readonly IPositionService _positionService;

    public PositionsController(IPositionService positionService)
    {
        _positionService = positionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Success(await _positionService.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _positionService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Create([FromBody] PositionRequest request, CancellationToken cancellationToken) =>
        Created(await _positionService.CreateAsync(request, cancellationToken), "Tạo chức vụ thành công.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Update(int id, [FromBody] PositionRequest request, CancellationToken cancellationToken) =>
        Success(await _positionService.UpdateAsync(id, request, cancellationToken), "Cập nhật chức vụ thành công.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _positionService.DeleteAsync(id, cancellationToken);
        return Success("Xóa chức vụ thành công.");
    }
}
