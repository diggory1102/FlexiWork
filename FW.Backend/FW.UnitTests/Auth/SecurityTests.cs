using FW.Application.Common.Exceptions;
using FW.Application.Common.Security;
using FW.Application.Common.Settings;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Security;
using FW.UnitTests.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace FW.UnitTests.Auth;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Admin@123")]
    [InlineData("abc12345")]
    public void Strong_passwords_are_valid(string password) => Assert.Empty(PasswordPolicy.Validate(password));

    [Theory]
    [InlineData("", "trống")]
    [InlineData("abc123", "8 ký tự")]
    [InlineData("abcdefgh", "chữ số")]
    [InlineData("12345678", "chữ cái")]
    [InlineData("abc 12345", "khoảng trắng")]
    public void Weak_passwords_report_reason(string password, string expected) =>
        Assert.Contains(PasswordPolicy.Validate(password), e => e.Contains(expected));
}

public class AccessScopeTests
{
    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Hr)]
    public void Admin_and_hr_access_every_branch(string role)
    {
        var user = new FakeCurrentUser(null, role);

        Assert.Null(user.GetBranchScope());
        Assert.True(user.CanAccessBranch(1));
        Assert.True(user.CanAccessBranch(99));
    }

    [Fact]
    public void Manager_is_limited_to_own_branch()
    {
        var manager = new FakeCurrentUser(1, RoleNames.Manager);

        Assert.Equal(1, manager.GetBranchScope());
        Assert.True(manager.CanAccessBranch(1));
        Assert.False(manager.CanAccessBranch(2));
        Assert.False(manager.CanAccessBranch(null));
        Assert.Throws<ForbiddenException>(() => manager.EnsureCanAccessBranch(2));
    }

    [Fact]
    public void Manager_without_branch_and_employee_have_no_branch_scope()
    {
        Assert.Throws<ForbiddenException>(() => new FakeCurrentUser(null, RoleNames.Manager).GetBranchScope());

        var employee = new FakeCurrentUser(1, RoleNames.Employee);
        Assert.Throws<ForbiddenException>(() => employee.GetBranchScope());
        Assert.False(employee.CanAccessBranch(1));
    }

    [Fact]
    public void Manager_who_is_also_hr_gets_company_wide_access()
    {
        var user = new FakeCurrentUser(1, RoleNames.Manager, RoleNames.Hr);
        Assert.Null(user.GetBranchScope());
        Assert.True(user.CanAccessBranch(2));
    }
}

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_is_salted_and_verifiable()
    {
        var first = _hasher.Hash("Admin@123");
        var second = _hasher.Hash("Admin@123");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("Admin@123", first));
        Assert.False(_hasher.Verify("admin@123", first));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-bcrypt-hash")]
    public void Invalid_stored_hash_is_treated_as_wrong_password(string storedHash) =>
        Assert.False(_hasher.Verify("Admin@123", storedHash));

    [Fact]
    public void Seed_password_hash_matches_documented_password()
    {
        const string seedHash = "$2a$11$4Qh91q1jTQsWXpeAgFjJdOEzx.XmGK.eTcYcFdxy5yC/NpZH9EZKO";
        Assert.True(_hasher.Verify("Admin@123", seedHash));
    }
}

public class JwtTokenGeneratorTests
{
    private static readonly JwtSettings Settings = new()
    {
        Secret = "unit-test-secret-key-at-least-32-characters!",
        Issuer = "FlexiWorkServer",
        Audience = "FlexiWorkClient",
        ExpiryMinutes = 60
    };

    [Fact]
    public async Task Token_contains_expected_claims_and_validates_with_same_settings()
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        var generator = new JwtTokenGenerator(Options.Create(Settings), clock);
        var user = new User { Id = 7, Email = "m@flexiwork.vn", FullName = "Quản Lý", EmployeeCode = "EMP007", BranchId = 3 };

        var token = generator.Generate(user, new[] { RoleNames.Manager, RoleNames.Hr });

        Assert.Equal(clock.UtcNow.AddMinutes(60), token.ExpiresAtUtc);

        // Kiểm tra token bằng đúng tham số mà API dùng (MapInboundClaims = false)
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token.Token,
            new TokenValidationParameters
            {
                ValidIssuer = Settings.Issuer,
                ValidAudience = Settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Secret)),
                NameClaimType = AppClaimTypes.Name,
                RoleClaimType = AppClaimTypes.Role
            });

        Assert.True(result.IsValid, result.Exception?.Message);
        var identity = result.ClaimsIdentity;
        Assert.Equal("7", identity.FindFirst(AppClaimTypes.UserId)!.Value);
        Assert.Equal("3", identity.FindFirst(AppClaimTypes.BranchId)!.Value);
        Assert.Equal("Quản Lý", identity.Name);
        Assert.Equal(new[] { RoleNames.Manager, RoleNames.Hr }, identity.FindAll(AppClaimTypes.Role).Select(c => c.Value));
    }

    [Fact]
    public async Task Token_signed_with_other_secret_is_rejected()
    {
        var generator = new JwtTokenGenerator(Options.Create(Settings), new FakeClock { UtcNow = DateTime.UtcNow });
        var token = generator.Generate(new User { Id = 1, Email = "a@b.vn", FullName = "A", EmployeeCode = "E1" }, Array.Empty<string>());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = Settings.Issuer,
            ValidAudience = Settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-secret-key-that-is-32-chars-long!!"))
        });

        Assert.False(result.IsValid);
    }
}
