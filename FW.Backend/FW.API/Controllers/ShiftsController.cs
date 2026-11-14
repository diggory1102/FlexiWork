using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Scheduling;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Khung ca làm việc (UC 1.3, UC 3.1).
/// Admin/HR: mọi ca. Quản lý chi nhánh: xem ca dùng chung + ca của chi nhánh mình, chỉ sửa ca của chi nhánh mình
/// (phạm vi kiểm tra ở ShiftService).
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
public class ShiftsController : ApiControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>Danh sách ca (phân trang, tìm theo mã/tên; lọc chi nhánh, có/không kèm ca dùng chung).</summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ShiftFilter filter, CancellationToken cancellationToken) =>
        Success(await _shiftService.GetPagedAsync(filter, cancellationToken));

    /// <summary>Chi tiết ca kèm cờ "đã được xếp lịch" (true = không sửa được giờ giấc).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _shiftService.GetByIdAsync(id, cancellationToken));

    /// <summary>Các ca chi nhánh được phép xếp lịch (ca dùng chung + ca riêng), sắp theo giờ bắt đầu.</summary>
    [HttpGet("available/{branchId:int}")]
    public async Task<IActionResult> GetAvailable(int branchId, CancellationToken cancellationToken) =>
        Success(await _shiftService.GetAvailableForBranchAsync(branchId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ShiftRequest request, CancellationToken cancellationToken) =>
        Created(await _shiftService.CreateAsync(request, cancellationToken), "Tạo ca làm việc thành công.");

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ShiftRequest request, CancellationToken cancellationToken) =>
        Success(await _shiftService.UpdateAsync(id, request, cancellationToken), "Cập nhật ca làm việc thành công.");

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _shiftService.DeleteAsync(id, cancellationToken);
        return Success("Xóa ca làm việc thành công.");
    }
}
