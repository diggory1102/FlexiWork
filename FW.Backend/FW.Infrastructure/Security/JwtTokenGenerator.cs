using System.Security.Claims;
using System.Text;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Security;
using FW.Application.Common.Settings;
using FW.Application.Features.Auth;
using FW.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FW.Infrastructure.Security;

/// <summary>
/// Phát hành JWT (HMAC-SHA256) chứa: sub (UserId), email, name, employee_code, pwd_stamp, branch_id và các role.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider _clock;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, IDateTimeProvider clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public AccessToken Generate(User user, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.Email, user.Email),
            new(AppClaimTypes.Name, user.FullName),
            new(AppClaimTypes.EmployeeCode, user.EmployeeCode),
            new(AppClaimTypes.SessionStamp, SessionStampService.StampOf(user).ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim(AppClaimTypes.BranchId, user.BranchId.Value.ToString()));
        }
        claims.AddRange(roles.Select(role => new Claim(AppClaimTypes.Role, role)));

        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_settings.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expires);
    }
}
