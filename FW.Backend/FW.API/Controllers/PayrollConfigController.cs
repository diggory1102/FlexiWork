using FW.Application.Features.Payroll;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Cấu hình lương (UC 8.1) — chỉ Admin, HR (lương là dữ liệu nhạy cảm, Quản lý chi nhánh không xem): hệ số OT, danh mục phụ cấp,
/// lương cơ bản / đơn giá giờ và phụ cấp riêng của từng nhân viên.
/// </summary>
[Authorize(Roles = RoleNames.AdminOrHr)]
[Route("api/payroll-config")]
public class PayrollConfigController : ApiControllerBase
{
    private readonly IPayrollConfigService _service;

    public PayrollConfigController(IPayrollConfigService service)
    {
        _service = service;
    }

    /// <summary>Hệ số OT, số công chuẩn, số giờ của một ngày công.</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken) =>
        Success(await _service.GetSettingsAsync(cancellationToken));

    /// <summary>Đặt hệ số OT: {overtimeMultiplier} (1 – 5).</summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePayrollSettingsRequest request, CancellationToken cancellationToken) =>
        Success(await _service.UpdateSettingsAsync(request, cancellationToken), "Đã lưu cấu hình lương.");

    // ---------------------------------------------------------------- Phụ cấp

    [HttpGet("allowances")]
    public async Task<IActionResult> GetAllowances([FromQuery] bool activeOnly, CancellationToken cancellationToken) =>
        Success(await _service.GetAllowancesAsync(activeOnly, cancellationToken));

    [HttpPost("allowances")]
    public async Task<IActionResult> CreateAllowance([FromBody] AllowanceRequest request, CancellationToken cancellationToken) =>
        Created(await _service.CreateAllowanceAsync(request, cancellationToken), "Tạo phụ cấp thành công.");

    [HttpPut("allowances/{id:int}")]
    public async Task<IActionResult> UpdateAllowance(int id, [FromBody] AllowanceRequest request, CancellationToken cancellationToken) =>
        Success(await _service.UpdateAllowanceAsync(id, request, cancellationToken), "Cập nhật phụ cấp thành công.");

    [HttpDelete("allowances/{id:int}")]
    public async Task<IActionResult> DeleteAllowance(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAllowanceAsync(id, cancellationToken);
        return Success("Xóa phụ cấp thành công.");
    }

    // ---------------------------------------------------------------- Theo nhân viên

    /// <summary>Lương của nhân viên: branchId, employmentType, configured (đủ / thiếu thông tin), search; phân trang.</summary>
    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees([FromQuery] PayrollEmployeeFilter filter, CancellationToken cancellationToken) =>
        Success(await _service.GetEmployeesAsync(filter, cancellationToken));

    [HttpGet("employees/{userId:int}")]
    public async Task<IActionResult> GetEmployee(int userId, CancellationToken cancellationToken) =>
        Success(await _service.GetEmployeeAsync(userId, cancellationToken));

    /// <summary>Đặt lương cơ bản và đơn giá giờ riêng: {baseSalary?, hourlyRate?} (bỏ trống = xóa).</summary>
    [HttpPut("employees/{userId:int}")]
    public async Task<IActionResult> UpdateEmployeePay(int userId, [FromBody] UpdateEmployeePayRequest request, CancellationToken cancellationToken) =>
        Success(await _service.UpdateEmployeePayAsync(userId, request, cancellationToken), "Đã lưu cấu hình lương của nhân viên.");

    /// <summary>Thay toàn bộ phụ cấp gán riêng của nhân viên: {items: [{allowanceId, amount?}]}.</summary>
    [HttpPut("employees/{userId:int}/allowances")]
    public async Task<IActionResult> SetEmployeeAllowances(int userId, [FromBody] SetEmployeeAllowancesRequest request, CancellationToken cancellationToken) =>
        Success(await _service.SetEmployeeAllowancesAsync(userId, request, cancellationToken), "Đã lưu phụ cấp của nhân viên.");
}
