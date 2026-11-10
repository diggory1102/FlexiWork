using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Organization;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Chi nhánh, tọa độ Geofence và Wi-Fi BSSID.
/// Xem: Admin, HR (toàn bộ), Manager (chi nhánh của mình). Thêm/sửa/xóa: Admin.
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
public class BranchesController : ApiControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>Danh sách chi nhánh (phân trang, tìm theo mã/tên/địa chỉ, lọc theo trạng thái).</summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] BranchFilter filter, CancellationToken cancellationToken) =>
        Success(await _branchService.GetPagedAsync(filter, cancellationToken));

    /// <summary>Chi tiết chi nhánh kèm danh sách Wi-Fi.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _branchService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create([FromBody] BranchRequest request, CancellationToken cancellationToken) =>
        Created(await _branchService.CreateAsync(request, cancellationToken), "Tạo chi nhánh thành công.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Update(int id, [FromBody] BranchRequest request, CancellationToken cancellationToken) =>
        Success(await _branchService.UpdateAsync(id, request, cancellationToken), "Cập nhật chi nhánh thành công.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _branchService.DeleteAsync(id, cancellationToken);
        return Success("Xóa chi nhánh thành công.");
    }

    // ------------------------------------------------------------------
    // Wi-Fi BSSID của chi nhánh
    // ------------------------------------------------------------------

    [HttpGet("{id:int}/wifis")]
    public async Task<IActionResult> GetWifis(int id, CancellationToken cancellationToken) =>
        Success(await _branchService.GetWifisAsync(id, cancellationToken));

    [HttpPost("{id:int}/wifis")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> AddWifi(int id, [FromBody] BranchWifiRequest request, CancellationToken cancellationToken) =>
        Created(await _branchService.AddWifiAsync(id, request, cancellationToken), "Thêm Wi-Fi thành công.");

    [HttpPut("{id:int}/wifis/{wifiId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateWifi(int id, int wifiId, [FromBody] BranchWifiRequest request, CancellationToken cancellationToken) =>
        Success(await _branchService.UpdateWifiAsync(id, wifiId, request, cancellationToken), "Cập nhật Wi-Fi thành công.");

    [HttpDelete("{id:int}/wifis/{wifiId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> DeleteWifi(int id, int wifiId, CancellationToken cancellationToken)
    {
        await _branchService.DeleteWifiAsync(id, wifiId, cancellationToken);
        return Success("Xóa Wi-Fi thành công.");
    }
}
