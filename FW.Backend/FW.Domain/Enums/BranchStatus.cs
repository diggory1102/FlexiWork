namespace FW.Domain.Enums;

/// <summary>
/// Trạng thái hoạt động của chi nhánh — khớp với cột branches.status.
/// </summary>
public static class BranchStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";

    public static readonly IReadOnlyList<string> All = new[] { Active, Inactive };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);
}
