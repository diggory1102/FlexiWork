namespace FW.Application.Features.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthUserDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đổi mật khẩu khi đã đăng nhập. Mọi token cũ bị thu hồi (các thiết bị khác bị đăng xuất),
    /// nên trả về phiên mới cho thiết bị đang thao tác.
    /// </summary>
    Task<LoginResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi OTP đặt lại mật khẩu qua email. Luôn kết thúc "thành công" kể cả khi email không tồn tại,
    /// để không lộ thông tin tài khoản nào có trong hệ thống.
    /// </summary>
    Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
