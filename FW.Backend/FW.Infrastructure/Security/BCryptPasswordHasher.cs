using FW.Application.Common.Interfaces;

namespace FW.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng BCrypt (có salt ngẫu nhiên, work factor 11).
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    public const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash trong CSDL không đúng định dạng BCrypt → coi như sai mật khẩu
            return false;
        }
    }
}
