namespace FW.Application.Common.Security;

/// <summary>
/// Quy tắc độ mạnh mật khẩu: tối thiểu 8 ký tự, có chữ cái và chữ số.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 64;

    /// <returns>Danh sách lỗi; rỗng nếu mật khẩu hợp lệ.</returns>
    public static List<string> Validate(string? password)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Mật khẩu không được để trống.");
            return errors;
        }

        if (password.Length < MinLength) errors.Add($"Mật khẩu phải có ít nhất {MinLength} ký tự.");
        if (password.Length > MaxLength) errors.Add($"Mật khẩu không được vượt quá {MaxLength} ký tự.");
        if (!password.Any(char.IsLetter)) errors.Add("Mật khẩu phải chứa ít nhất một chữ cái.");
        if (!password.Any(char.IsDigit)) errors.Add("Mật khẩu phải chứa ít nhất một chữ số.");
        if (password.Any(char.IsWhiteSpace)) errors.Add("Mật khẩu không được chứa khoảng trắng.");

        return errors;
    }
}
