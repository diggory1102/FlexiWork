using System.Security.Cryptography;

namespace FW.Application.Common.Security;

/// <summary>
/// Sinh mật khẩu tạm ngẫu nhiên (an toàn mật mã) thỏa <see cref="PasswordPolicy"/>.
/// Bỏ các ký tự dễ nhầm (0/O, 1/l/I) vì người dùng phải gõ lại từ email.
/// </summary>
public static class PasswordGenerator
{
    private const string Letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "@#$%";

    public static string Generate(int length = 12)
    {
        if (length < PasswordPolicy.MinLength) length = PasswordPolicy.MinLength;

        var all = Letters + Digits + Symbols;
        var chars = new char[length];

        // Bảo đảm có ít nhất 1 chữ cái, 1 chữ số, 1 ký tự đặc biệt
        chars[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        chars[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[2] = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
        for (var i = 3; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Xáo trộn để vị trí các nhóm ký tự không cố định
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
