namespace FW.Domain.Enums;

/// <summary>
/// Trạng thái tài khoản nhân sự — khớp với cột users.status.
/// Dùng hằng chuỗi thay vì enum C# vì Dapper ghi enum dưới dạng số nguyên vào cột VARCHAR.
/// </summary>
public static class UserStatus
{
    /// <summary>Đang hoạt động, được phép đăng nhập.</summary>
    public const string Active = "active";

    /// <summary>Bị khóa tạm thời, không được đăng nhập.</summary>
    public const string Suspended = "suspended";

    /// <summary>Đã nghỉ việc.</summary>
    public const string Left = "left";

    public static readonly IReadOnlyList<string> All = new[] { Active, Suspended, Left };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);
}
