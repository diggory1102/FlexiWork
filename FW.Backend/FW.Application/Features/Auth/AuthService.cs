using System.Security.Cryptography;
using System.Text;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Auth;

/// <summary>
/// Nghiệp vụ xác thực: đăng nhập (chống dò mật khẩu), đổi mật khẩu, quên mật khẩu bằng OTP qua email.
/// </summary>
public class AuthService : IAuthService
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(15);

    public const int MaxOtpAttempts = 5;
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(60);

    private const string InvalidCredentialsMessage = "Email hoặc mật khẩu không chính xác.";
    private const string InvalidOtpMessage = "Mã OTP không hợp lệ hoặc đã hết hạn.";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ICacheService _cache;
    private readonly IEmailService _email;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ICacheService cache,
        IEmailService email,
        IDateTimeProvider clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _cache = cache;
        _email = email;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    // Đăng nhập
    // ------------------------------------------------------------------

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var failKey = LoginFailKey(email);

        var failures = await _cache.GetAsync<int?>(failKey, cancellationToken) ?? 0;
        if (failures >= MaxFailedLogins)
        {
            throw new UnauthorizedException(
                $"Tài khoản tạm thời bị khóa đăng nhập {LoginLockDuration.TotalMinutes:0} phút do nhập sai mật khẩu nhiều lần.");
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Ghi nhận cả trường hợp email không tồn tại để không phân biệt được hai trường hợp
            await _cache.SetAsync<int?>(failKey, failures + 1, LoginLockDuration, cancellationToken);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        EnsureAccountActive(user);
        await _cache.RemoveAsync(failKey, cancellationToken);

        var roles = await _users.GetRoleNamesAsync(user.Id, cancellationToken);
        var token = _tokenGenerator.Generate(user, roles);

        return new LoginResponse(token.Token, token.ExpiresAtUtc, ToDto(user, roles));
    }

    public async Task<AuthUserDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        var roles = await _users.GetRoleNamesAsync(user.Id, cancellationToken);
        return ToDto(user, roles);
    }

    // ------------------------------------------------------------------
    // Đổi mật khẩu (đã đăng nhập)
    // ------------------------------------------------------------------

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BadRequestException("Mật khẩu hiện tại không chính xác.");
        }
        if (request.CurrentPassword == request.NewPassword)
        {
            throw new BadRequestException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        await SetPasswordAsync(user, request.NewPassword, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Quên mật khẩu bằng OTP
    // ------------------------------------------------------------------

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        // Chống spam: mỗi email chỉ được yêu cầu OTP một lần trong khoảng chờ
        var cooldownKey = OtpCooldownKey(email);
        if (await _cache.ExistsAsync(cooldownKey, cancellationToken)) return;

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user == null || !user.IsActive) return;

        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var entry = new OtpEntry(HashOtp(email, otp), 0, _clock.UtcNow.Add(OtpLifetime));

        await _cache.SetAsync(OtpKey(email), entry, OtpLifetime, cancellationToken);
        await _cache.SetAsync(cooldownKey, true, OtpResendCooldown, cancellationToken);

        await _email.SendAsync(
            user.Email,
            "[FlexiWork] Mã OTP đặt lại mật khẩu",
            AuthEmailTemplates.PasswordResetOtp(user.FullName, otp, (int)OtpLifetime.TotalMinutes),
            cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var otpKey = OtpKey(email);

        var entry = await _cache.GetAsync<OtpEntry>(otpKey, cancellationToken);
        var remaining = entry == null ? TimeSpan.Zero : entry.ExpiresAtUtc - _clock.UtcNow;
        if (entry == null || remaining <= TimeSpan.Zero)
        {
            throw new BadRequestException(InvalidOtpMessage);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(entry.OtpHash), Encoding.UTF8.GetBytes(HashOtp(email, request.Otp))))
        {
            var attempts = entry.FailedAttempts + 1;
            if (attempts >= MaxOtpAttempts)
            {
                await _cache.RemoveAsync(otpKey, cancellationToken);
                throw new BadRequestException("Nhập sai OTP quá nhiều lần. Vui lòng yêu cầu mã mới.");
            }

            await _cache.SetAsync(otpKey, entry with { FailedAttempts = attempts }, remaining, cancellationToken);
            throw new BadRequestException(InvalidOtpMessage);
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user == null) throw new BadRequestException(InvalidOtpMessage);
        EnsureAccountActive(user);

        await SetPasswordAsync(user, request.NewPassword, cancellationToken);

        // OTP chỉ dùng một lần; đồng thời gỡ khóa đăng nhập nếu đang bị khóa
        await _cache.RemoveAsync(otpKey, cancellationToken);
        await _cache.RemoveAsync(LoginFailKey(email), cancellationToken);
    }

    // ------------------------------------------------------------------
    // Hàm hỗ trợ
    // ------------------------------------------------------------------

    private async Task SetPasswordAsync(User user, string newPassword, CancellationToken cancellationToken)
    {
        var errors = PasswordPolicy.Validate(newPassword);
        if (errors.Count > 0)
        {
            throw new BadRequestException("Mật khẩu mới chưa đủ mạnh.", errors);
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.MustChangePassword = false; // người dùng đã tự đặt mật khẩu, không còn dùng mật khẩu tạm
        await _users.UpdateAsync(user, cancellationToken);
    }

    private async Task<User> GetActiveUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
                   ?? throw new UnauthorizedException();
        EnsureAccountActive(user);
        return user;
    }

    private static void EnsureAccountActive(User user)
    {
        switch (user.Status)
        {
            case UserStatus.Active:
                return;
            case UserStatus.Suspended:
                throw new UnauthorizedException("Tài khoản đã bị khóa. Vui lòng liên hệ bộ phận Nhân sự.");
            default:
                throw new UnauthorizedException("Tài khoản không còn hoạt động.");
        }
    }

    private static AuthUserDto ToDto(User user, IReadOnlyList<string> roles) =>
        new(user.Id, user.EmployeeCode, user.FullName, user.Email, user.AvatarUrl, user.BranchId, roles, user.MustChangePassword);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string HashOtp(string email, string otp) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email}:{otp}")));

    private static string LoginFailKey(string email) => $"auth:login-fail:{email}";
    private static string OtpKey(string email) => $"auth:otp:{email}";
    private static string OtpCooldownKey(string email) => $"auth:otp-cooldown:{email}";

    /// <summary>OTP lưu trong cache dưới dạng băm (SHA-256), không lưu mã gốc.</summary>
    public record OtpEntry(string OtpHash, int FailedAttempts, DateTime ExpiresAtUtc);
}
