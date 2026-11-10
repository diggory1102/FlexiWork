using System.Text.RegularExpressions;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using FW.Application.Features.Auth;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Security;
using FW.Infrastructure.Services;
using FW.Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FW.IntegrationTests.Auth;

/// <summary>
/// Luồng xác thực đầy đủ trên MySQL thật với các cài đặt thật (BCrypt, JWT, cache bộ nhớ).
/// Xác nhận tài khoản mẫu trong seed đăng nhập được bằng mật khẩu đã công bố "Admin@123".
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AuthFlowTests
{
    private const string SeedPassword = "Admin@123";
    private readonly DatabaseFixture _db;

    public AuthFlowTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class CapturingEmail : IEmailService
    {
        public string LastBody { get; private set; } = string.Empty;

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            LastBody = htmlBody;
            return Task.CompletedTask;
        }
    }

    private static AuthService CreateService(DbSession session, IEmailService email)
    {
        var clock = new DateTimeProvider();
        var jwt = Options.Create(new JwtSettings
        {
            Secret = "integration-test-secret-key-at-least-32-chars",
            Issuer = "FlexiWorkServer",
            Audience = "FlexiWorkClient",
            ExpiryMinutes = 60
        });

        return new AuthService(
            new UserRepository(session),
            new BCryptPasswordHasher(),
            new JwtTokenGenerator(jwt, clock),
            new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())),
            email,
            clock);
    }

    [SkippableTheory]
    [InlineData("admin@flexiwork.vn", RoleNames.Admin, 1)]
    [InlineData("manager.cg@flexiwork.vn", RoleNames.Manager, 1)]
    [InlineData("nhanvien.cg@flexiwork.vn", RoleNames.Employee, 1)]
    [InlineData("hr@flexiwork.vn", RoleNames.Hr, null)]
    public async Task Seed_accounts_can_login_with_documented_password(string email, string role, int? branchId)
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();

        var result = await CreateService(session, new CapturingEmail())
            .LoginAsync(new LoginRequest { Email = email, Password = SeedPassword });

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.Equal(new[] { role }, result.User.Roles);
        Assert.Equal(branchId, result.User.BranchId);
    }

    [SkippableFact]
    public async Task Forgot_and_reset_password_updates_hash_in_database()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var users = new UserRepository(session);
        var hasher = new BCryptPasswordHasher();
        var email = new CapturingEmail();
        var service = CreateService(session, email);

        var code = $"R{Guid.NewGuid():N}"[..20];
        var address = $"{code}@test.local";
        await users.AddAsync(new User
        {
            EmployeeCode = code, Email = address, FullName = "Đặt lại mật khẩu", PasswordHash = hasher.Hash("OldPass123")
        });

        await service.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = address });
        var otp = Regex.Match(email.LastBody, @"\b\d{6}\b").Value;
        await service.ResetPasswordAsync(new ResetPasswordRequest { Email = address, Otp = otp, NewPassword = "NewPass456" });

        // Đọc lại từ CSDL bằng phiên mới
        await using var verify = _db.CreateSession();
        var saved = await new UserRepository(verify).GetByEmailAsync(address);
        Assert.True(hasher.Verify("NewPass456", saved!.PasswordHash));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Email = address, Password = "OldPass123" }));
        Assert.NotNull(await service.LoginAsync(new LoginRequest { Email = address, Password = "NewPass456" }));
    }
}
