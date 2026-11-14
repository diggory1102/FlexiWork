namespace FW.Domain.Enums;

/// <summary>
/// Trạng thái thực hiện của một ca đã xếp — khớp với cột schedules.status.
/// (Nháp/công bố là cờ riêng schedules.is_published, không nằm trong trạng thái này.)
/// </summary>
public static class ScheduleStatus
{
    /// <summary>Đã xếp, chưa tới giờ làm hoặc chưa check-in.</summary>
    public const string Scheduled = "scheduled";

    /// <summary>Đã check-in, đang trong ca.</summary>
    public const string InProgress = "in_progress";

    /// <summary>Đã check-out.</summary>
    public const string Completed = "completed";

    /// <summary>Hết ca mà không check-in (tác vụ nền đánh dấu — UC 4.7).</summary>
    public const string Absent = "absent";

    /// <summary>Ca đã công bố nhưng bị hủy (giữ lại để đối soát, không tính công).</summary>
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = new[] { Scheduled, InProgress, Completed, Absent, Cancelled };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);
}
