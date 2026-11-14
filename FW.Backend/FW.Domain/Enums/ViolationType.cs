namespace FW.Domain.Enums;

/// <summary>
/// Loại vi phạm giờ giấc — khớp với cột violation_rules.violation_type.
/// </summary>
public static class ViolationType
{
    /// <summary>Đi muộn: check-in sau giờ bắt đầu ca (vượt số phút cho phép của ca).</summary>
    public const string Late = "late";

    /// <summary>Về sớm: check-out trước giờ kết thúc ca (vượt số phút cho phép của ca).</summary>
    public const string EarlyLeave = "early_leave";

    public static readonly IReadOnlyList<string> All = new[] { Late, EarlyLeave };

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}
