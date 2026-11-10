using FW.Application.Common.Models;
using FW.Application.Features.Organization;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Phòng ban. Xem: Admin, HR, Manager. Thêm/sửa/xóa: Admin, HR.
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
public class DepartmentsController : ApiControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        Success(await _departmentService.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _departmentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Create([FromBody] DepartmentRequest request, CancellationToken cancellationToken) =>
        Created(await _departmentService.CreateAsync(request, cancellationToken), "Tạo phòng ban thành công.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Update(int id, [FromBody] DepartmentRequest request, CancellationToken cancellationToken) =>
        Success(await _departmentService.UpdateAsync(id, request, cancellationToken), "Cập nhật phòng ban thành công.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _departmentService.DeleteAsync(id, cancellationToken);
        return Success("Xóa phòng ban thành công.");
    }
}
