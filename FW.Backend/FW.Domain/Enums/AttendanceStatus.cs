namespace FW.Domain.Enums;

/// <summary>
/// Kết quả đối soát của một lượt chấm công — khớp với cột attendance_logs.status.
/// </summary>
public static class AttendanceStatus
{
    /// <summary>Đạt đủ các lớp xác minh — tính công bình thường.</summary>
    public const string Valid = "valid";

    /// <summary>Lớp GPS và khuôn mặt đạt nhưng lớp Wi-Fi không đạt — chờ Quản lý / HR duyệt.</summary>
    public const string PendingApproval = "pending_approval";

    /// <summary>Quản lý / HR từ chối khi duyệt — không tính công.</summary>
    public const string Invalid = "invalid";

    public static readonly IReadOnlyList<string> All = new[] { Valid, PendingApproval, Invalid };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);
}
