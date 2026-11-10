using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Users;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Hồ sơ nhân sự & tài khoản.
/// Xem: Admin, HR (toàn bộ), Manager (chi nhánh mình). Thêm/sửa/khóa/cấp lại mật khẩu/xóa: Admin, HR.
/// Đổi vai trò: chỉ Admin (ma trận RBAC: "Cấu hình vai trò & Phân quyền").
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
public class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Danh sách nhân sự: tìm theo mã/email/họ tên/SĐT; lọc theo chi nhánh, phòng ban, chức vụ, trạng thái, vai trò.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] UserFilter filter, CancellationToken cancellationToken) =>
        Success(await _userService.GetPagedAsync(filter, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _userService.GetByIdAsync(id, cancellationToken));

    /// <summary>Tạo tài khoản: hệ thống sinh mật khẩu tạm và gửi email mời đăng nhập.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(request, cancellationToken);
        var message = result.InvitationSent
            ? "Tạo tài khoản thành công. Thông tin đăng nhập đã được gửi tới email nhân viên."
            : "Tạo tài khoản thành công nhưng gửi email thất bại. Hãy dùng chức năng \"Cấp lại mật khẩu\".";
        return Created(result, message);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Update(int id, [FromBody] UserProfileRequest request, CancellationToken cancellationToken) =>
        Success(await _userService.UpdateAsync(id, request, cancellationToken), "Cập nhật hồ sơ nhân sự thành công.");

    /// <summary>Khóa / mở khóa / đánh dấu nghỉ việc (active, suspended, left).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken) =>
        Success(await _userService.UpdateStatusAsync(id, request, cancellationToken), "Cập nhật trạng thái tài khoản thành công.");

    [HttpPut("{id:int}/roles")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateRoles(int id, [FromBody] UpdateUserRolesRequest request, CancellationToken cancellationToken) =>
        Success(await _userService.UpdateRolesAsync(id, request, cancellationToken), "Cập nhật vai trò thành công.");

    /// <summary>Cấp lại mật khẩu tạm và gửi email (bắt buộc đổi ở lần đăng nhập tới).</summary>
    [HttpPost("{id:int}/reissue-password")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> ReissuePassword(int id, CancellationToken cancellationToken)
    {
        var sent = await _userService.ReissuePasswordAsync(id, cancellationToken);
        return Success(sent
            ? "Đã cấp lại mật khẩu tạm và gửi email cho nhân viên."
            : "Đã cấp lại mật khẩu tạm nhưng gửi email thất bại. Vui lòng kiểm tra cấu hình SMTP.");
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHr)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return Success("Xóa hồ sơ nhân sự thành công.");
    }
}
