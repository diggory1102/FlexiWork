using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Xác thực: đăng nhập, thông tin tài khoản, đổi mật khẩu, quên mật khẩu bằng OTP.
/// </summary>
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>Đăng nhập bằng email & mật khẩu, nhận JWT access token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Success(result, "Đăng nhập thành công.");
    }

    /// <summary>Thông tin tài khoản đang đăng nhập.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var profile = await _authService.GetProfileAsync(RequireUserId(), cancellationToken);
        return Success(profile);
    }

    /// <summary>Đổi mật khẩu; trả về token mới cho thiết bị hiện tại, các phiên khác bị đăng xuất.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        // Trả phiên mới: token cũ đã bị thu hồi cùng với các thiết bị khác
        var session = await _authService.ChangePasswordAsync(RequireUserId(), request, cancellationToken);
        return Success(session, "Đổi mật khẩu thành công. Các thiết bị khác đã được đăng xuất.");
    }

    /// <summary>Gửi OTP đặt lại mật khẩu về email (luôn trả thành công để không lộ email nào tồn tại).</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.RequestPasswordResetAsync(request, cancellationToken);
        return Success("Nếu email tồn tại trong hệ thống, mã OTP đã được gửi. Mã có hiệu lực trong 5 phút.");
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return Success("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    private int RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedException();
}
