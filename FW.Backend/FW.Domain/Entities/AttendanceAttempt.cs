using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Một lượt bấm chấm công (bảng attendance_attempts), kể cả lượt bị từ chối — <see cref="AttendanceLog"/> chỉ giữ lượt được ghi nhận.
/// Lưu kết quả từng lớp xác minh và thời gian xử lý để thống kê thí nghiệm thực tế (Task 6.5).
/// </summary>
public class AttendanceAttempt : BaseEntity<long>
{
    public int UserId { get; set; }

    /// <summary>Chi nhánh của ca; null nếu lượt bị chặn trước khi xác định được ca.</summary>
    public int? BranchId { get; set; }
    public long? ScheduleId { get; set; }

    /// <summary>check_in | check_out.</summary>
    public string Action { get; set; } = string.Empty;
    public DateTime AttemptedAt { get; set; }

    /// <summary>Xem <see cref="AttemptOutcome"/>.</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Xem <see cref="AttemptStage"/> — lớp đang xử lý khi lượt kết thúc (lớp chặn nếu bị từ chối).</summary>
    public string Stage { get; set; } = string.Empty;
    public string? Message { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? Accuracy { get; set; }
    public double? Distance { get; set; }
    public bool IsMocked { get; set; }
    public string? Bssid { get; set; }

    /// <summary>matched | not_matched | no_wifi_info | not_required.</summary>
    public string? WifiStatus { get; set; }
    public double? FaceScore { get; set; }
    public double? FaceThreshold { get; set; }

    /// <summary>Thời gian máy chủ xử lý toàn bộ lượt (ms).</summary>
    public int DurationMs { get; set; }
    public string? DeviceId { get; set; }
}

public static class AttemptOutcome
{
    public const string Accepted = "accepted";
    public const string PendingApproval = "pending_approval";
    public const string Rejected = "rejected";

    /// <summary>Lỗi hệ thống không lường trước (không phải bị chặn có chủ đích).</summary>
    public const string Error = "error";
}

public static class AttemptStage
{
    public const string Validate = "validate";
    public const string Time = "time";
    public const string Gps = "gps";
    public const string Face = "face";
    public const string Wifi = "wifi";
    public const string Save = "save";
}
