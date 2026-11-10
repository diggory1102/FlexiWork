namespace FW.Application.Features.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthUserDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi OTP đặt lại mật khẩu qua email. Luôn kết thúc "thành công" kể cả khi email không tồn tại,
    /// để không lộ thông tin tài khoản nào có trong hệ thống.
    /// </summary>
    Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
