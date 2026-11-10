namespace FW.Application.Common.Interfaces;

/// <summary>
/// Băm và xác minh mật khẩu (cài đặt: BCrypt).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
