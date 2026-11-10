namespace FW.Domain.Enums;

/// <summary>
/// Loại hợp đồng lao động — khớp với cột users.employment_type.
/// </summary>
public static class EmploymentType
{
    /// <summary>Toàn thời gian.</summary>
    public const string FullTime = "full_time";

    /// <summary>Bán thời gian (thường làm theo ca ngắn).</summary>
    public const string PartTime = "part_time";

    public static readonly IReadOnlyList<string> All = new[] { FullTime, PartTime };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
