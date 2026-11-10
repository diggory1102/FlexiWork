using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces;

/// <summary>
/// Phát hành JWT access token cho người dùng đã xác thực.
/// </summary>
public interface IJwtTokenGenerator
{
    AccessToken Generate(User user, IReadOnlyList<string> roles);
}

/// <param name="Token">Chuỗi JWT.</param>
/// <param name="ExpiresAtUtc">Thời điểm hết hạn (UTC).</param>
public record AccessToken(string Token, DateTime ExpiresAtUtc);
