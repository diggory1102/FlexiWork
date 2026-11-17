namespace FW.Domain.Enums;

/// <summary>
/// Trạng thái bảng công tháng (cột monthly_timesheets.status):
/// <c>draft</c> hệ thống tính, tính lại được → <c>confirmed</c> Quản lý xác nhận → <c>locked</c> HR khóa (số liệu cố định, chặn duyệt thêm
/// chấm công / đơn của tháng đó) → <c>finalized</c> đã chốt lương (Task 5.5, không mở lại được).
/// </summary>
public static class TimesheetStatus
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
    public const string Locked = "locked";
    public const string Finalized = "finalized";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Confirmed, Locked, Finalized };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    /// <summary>Số liệu đã cố định: không tính lại, không duyệt thêm chấm công / đơn của tháng này.</summary>
    public static bool IsFrozen(string status) => status is Locked or Finalized;
}
