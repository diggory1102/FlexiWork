using System.Text.RegularExpressions;
using FW.Application.Common.Exceptions;
using FW.Application.Features.Auth;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.UnitTests.Fakes;

namespace FW.UnitTests.Auth;

public class AuthServiceTests
{
    private const string Password = "Secret123";

    private readonly FakeClock _clock = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEmailService _email = new();
    private readonly FakeCache _cache;
    private readonly AuthService _service;
    private readonly User _employee;

    public AuthServiceTests()
    {
        _cache = new FakeCache(_clock);
        _service = new AuthService(_users, _hasher, new FakeTokenGenerator(), _cache, _email, _clock);
        _employee = _users.Add(NewUser("nv@flexiwork.vn"), RoleNames.Employee);
    }

    private User NewUser(string email, string status = UserStatus.Active) => new()
    {
        EmployeeCode = "EMP" + (_users.Users.Count + 1),
        Email = email,
        FullName = "Nhân viên",
        PasswordHash = _hasher.Hash(Password),
        BranchId = 1,
        Status = status
    };

    private Task<LoginResponse> Login(string email, string password) =>
        _service.LoginAsync(new LoginRequest { Email = email, Password = password });

    // ---------------- Đăng nhập ----------------

    [Fact]
    public async Task Login_succeeds_with_roles_and_normalizes_email()
    {
        var result = await Login("  NV@FlexiWork.vn ", Password);

        Assert.Equal($"token-for-{_employee.Id}", result.AccessToken);
        Assert.Equal(_employee.Id, result.User.Id);
        Assert.Equal(new[] { RoleNames.Employee }, result.User.Roles);
        Assert.Equal(1, result.User.BranchId);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_same_message()
    {
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", "Wrong123"));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(() => Login("ai@flexiwork.vn", Password));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Theory]
    [InlineData(UserStatus.Suspended, "bị khóa")]
    [InlineData(UserStatus.Left, "không còn hoạt động")]
    public async Task Inactive_account_cannot_login(string status, string expectedMessagePart)
    {
        _users.Add(NewUser("off@flexiwork.vn", status), RoleNames.Employee);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Login("off@flexiwork.vn", Password));
        Assert.Contains(expectedMessagePart, ex.Message);
    }

    [Fact]
    public async Task Login_is_locked_after_too_many_failures_then_unlocks_after_lock_duration()
    {
        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", "Wrong123"));
        }

        // Đang bị khóa: kể cả mật khẩu đúng cũng bị từ chối
        var locked = await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", Password));
        Assert.Contains("tạm thời bị khóa", locked.Message);

        _clock.Advance(AuthService.LoginLockDuration + TimeSpan.FromSeconds(1));
        Assert.NotNull(await Login("nv@flexiwork.vn", Password));
    }

    [Fact]
    public async Task Successful_login_resets_failure_counter()
    {
        for (var i = 0; i < AuthService.MaxFailedLogins - 1; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", "Wrong123"));
        }
        await Login("nv@flexiwork.vn", Password);

        // Bộ đếm đã về 0 nên sai thêm 1 lần nữa chưa bị khóa
        await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", "Wrong123"));
        Assert.NotNull(await Login("nv@flexiwork.vn", Password));
    }

    // ---------------- Đổi mật khẩu ----------------

    [Fact]
    public async Task Change_password_requires_correct_current_password_and_strong_new_password()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _service.ChangePasswordAsync(_employee.Id,
            new ChangePasswordRequest { CurrentPassword = "Wrong123", NewPassword = "NewPass123" }));

        var weak = await Assert.ThrowsAsync<BadRequestException>(() => _service.ChangePasswordAsync(_employee.Id,
            new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "short" }));
        Assert.NotEmpty(weak.Errors!);

        await _service.ChangePasswordAsync(_employee.Id,
            new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "NewPass123" });

        Assert.NotNull(await Login("nv@flexiwork.vn", "NewPass123"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", Password));
    }

    [Fact]
    public async Task Temporary_password_flag_is_reported_at_login_and_cleared_after_change()
    {
        _employee.MustChangePassword = true;
        Assert.True((await Login("nv@flexiwork.vn", Password)).User.MustChangePassword);

        await _service.ChangePasswordAsync(_employee.Id,
            new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "NewPass123" });

        Assert.False(_employee.MustChangePassword);
        Assert.False((await Login("nv@flexiwork.vn", "NewPass123")).User.MustChangePassword);
    }

    // ---------------- Quên mật khẩu (OTP) ----------------

    private async Task<string> RequestOtp(string email = "nv@flexiwork.vn")
    {
        await _service.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = email });
        var body = _email.Sent.Last().Body;
        return Regex.Match(body, @"\b\d{6}\b").Value;
    }

    private Task Reset(string otp, string newPassword = "Reset1234", string email = "nv@flexiwork.vn") =>
        _service.ResetPasswordAsync(new ResetPasswordRequest { Email = email, Otp = otp, NewPassword = newPassword });

    [Fact]
    public async Task Reset_password_with_valid_otp_works_only_once()
    {
        var otp = await RequestOtp();
        Assert.Matches(@"^\d{6}$", otp);
        Assert.Equal("nv@flexiwork.vn", _email.Sent.Single().To);

        await Reset(otp);
        Assert.NotNull(await Login("nv@flexiwork.vn", "Reset1234"));

        // OTP đã dùng thì không dùng lại được
        await Assert.ThrowsAsync<BadRequestException>(() => Reset(otp, "Another123"));
    }

    [Fact]
    public async Task Otp_expires_after_lifetime()
    {
        var otp = await RequestOtp();
        _clock.Advance(AuthService.OtpLifetime + TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<BadRequestException>(() => Reset(otp));
    }

    [Fact]
    public async Task Otp_is_invalidated_after_too_many_wrong_attempts()
    {
        var otp = await RequestOtp();
        var wrong = otp == "000000" ? "111111" : "000000";

        for (var i = 0; i < AuthService.MaxOtpAttempts - 1; i++)
        {
            await Assert.ThrowsAsync<BadRequestException>(() => Reset(wrong));
        }
        var last = await Assert.ThrowsAsync<BadRequestException>(() => Reset(wrong));
        Assert.Contains("quá nhiều lần", last.Message);

        // Kể cả OTP đúng cũng không còn dùng được
        await Assert.ThrowsAsync<BadRequestException>(() => Reset(otp));
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_unknown_email_and_respects_cooldown()
    {
        await _service.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = "khongton@flexiwork.vn" });
        Assert.Empty(_email.Sent);

        await RequestOtp();
        await _service.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = "nv@flexiwork.vn" });
        Assert.Single(_email.Sent); // lần 2 trong thời gian chờ không gửi thêm

        _clock.Advance(AuthService.OtpResendCooldown + TimeSpan.FromSeconds(1));
        await RequestOtp();
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task Reset_password_unlocks_login()
    {
        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => Login("nv@flexiwork.vn", "Wrong123"));
        }

        await Reset(await RequestOtp());
        Assert.NotNull(await Login("nv@flexiwork.vn", "Reset1234"));
    }

    [Fact]
    public async Task Otp_is_not_stored_in_plain_text()
    {
        var otp = await RequestOtp();
        var entry = await _cache.GetAsync<AuthService.OtpEntry>("auth:otp:nv@flexiwork.vn");

        Assert.NotNull(entry);
        Assert.DoesNotContain(otp, entry!.OtpHash);
    }
}
