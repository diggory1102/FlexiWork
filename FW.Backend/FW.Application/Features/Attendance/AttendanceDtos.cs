using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.Application.Features.Attendance;

/// <summary>Dữ liệu điện thoại gửi kèm ảnh selfie khi check-in / check-out (form-data).</summary>
public class AttendanceCaptureRequest
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Sai số vị trí (m) do hệ điều hành báo cáo.</summary>
    public double? Accuracy { get; set; }

    /// <summary>BSSID Wi-Fi đang kết nối; bỏ trống nếu không bật Wi-Fi / không đọc được.</summary>
    public string? Bssid { get; set; }

    /// <summary>Mã thiết bị (Android ID / iOS identifierForVendor).</summary>
    public string? DeviceId { get; set; }

    /// <summary>Hệ điều hành báo vị trí đến từ ứng dụng giả lập (cờ <c>mocked</c> của expo-location).</summary>
    public bool IsMocked { get; set; }
}

public static class AttendanceActions
{
    public const string CheckIn = "check_in";
    public const string CheckOut = "check_out";
    public const string None = "none";
}

public record GpsLayerDto(bool Passed, double DistanceMeters, int RadiusMeters, double? AccuracyMeters);

/// <param name="Status">matched, not_matched, no_wifi_info, not_required.</param>
public record WifiLayerDto(bool Passed, string Status, string? Bssid)
{
    public static WifiLayerDto From(WifiCheckResult r) => new(r.IsPassed, StatusName(r.Status), r.Bssid);

    public static string StatusName(WifiCheckStatus status) => status switch
    {
        WifiCheckStatus.Matched => "matched",
        WifiCheckStatus.NotMatched => "not_matched",
        WifiCheckStatus.NoWifiInfo => "no_wifi_info",
        _ => "not_required"
    };
}

public record FaceLayerDto(bool Passed, double Score, double Threshold);

/// <summary>Kết quả một lượt check-in / check-out đã được ghi nhận (kèm kết quả từng lớp xác minh).</summary>
public record AttendanceResultDto(
    long LogId,
    long? ScheduleId,
    string Action,
    DateTime Time,
    string ShiftName,
    string Status,
    int LateMinutes,
    int EarlyMinutes,
    decimal ActualWorkHours,
    GpsLayerDto Gps,
    WifiLayerDto Wifi,
    FaceLayerDto Face,
    IReadOnlyList<string> Warnings,
    string Message);

public record AttendanceLogSummaryDto(
    long Id, DateTime CheckinTime, DateTime? CheckoutTime, string Status,
    int LateMinutes, int EarlyMinutes, decimal ActualWorkHours, string? Note)
{
    public static AttendanceLogSummaryDto From(AttendanceLog l) =>
        new(l.Id, l.CheckinTime, l.CheckoutTime, l.Status, l.LateMinutes, l.EarlyMinutes, l.ActualWorkHours, l.Note);
}

/// <summary>Một ca hôm nay (hoặc ca đêm qua chưa kết thúc) kèm trạng thái chấm công.</summary>
public record AttendanceShiftDto(
    long ScheduleId,
    DateTime WorkDate,
    string ShiftCode,
    string ShiftName,
    DateTime StartAt,
    DateTime EndAt,
    DateTime CheckInOpensAt,
    DateTime CheckOutDeadline,
    string ScheduleStatus,
    AttendanceLogSummaryDto? Log);

/// <summary>Chi nhánh của ca cần chấm công — để ứng dụng hiển thị khoảng cách trước khi chụp ảnh.</summary>
public record AttendanceBranchDto(int Id, string Name, decimal Latitude, decimal Longitude, int Radius, bool RequiresWifi);

/// <summary>Màn hình chấm công: việc cần làm tiếp theo (check-in / check-out / không có) và các ca trong ngày.</summary>
public record AttendanceTodayDto(
    DateTime Now,
    bool FaceRegistered,
    string NextAction,
    long? ActionScheduleId,
    string Message,
    AttendanceBranchDto? Branch,
    IReadOnlyList<AttendanceShiftDto> Shifts);

// ---------------------------------------------------------------- Tra cứu & duyệt (Task 3.3)

/// <summary>Bộ lọc nhật ký chấm công (query string). Khoảng ngày theo ngày công.</summary>
public class AttendanceLogFilter : FW.Application.Common.Models.PagedQuery
{
    /// <summary>Admin/HR: bỏ trống = mọi chi nhánh; Quản lý: bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    /// <summary>valid, pending_approval, invalid.</summary>
    public string? Status { get; set; }
}

/// <summary>Quản lý / HR duyệt một lượt chấm công đang chờ duyệt (UC 4.6).</summary>
public class ReviewAttendanceRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng chọn chấp nhận hoặc từ chối.")]
    public bool? Approved { get; set; }

    /// <summary>Bắt buộc khi từ chối.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

/// <summary>Minh chứng của một lượt vào hoặc ra.</summary>
public record AttendanceEvidenceDto(
    DateTime Time, string? ImageUrl, decimal? Latitude, decimal? Longitude, double? AccuracyMeters, double? DistanceMeters,
    string? Bssid, bool WifiMatched, bool FaceMatched, double? FaceScore, string? DeviceId);

/// <summary>Một dòng nhật ký chấm công (bảng tra cứu).</summary>
public record AttendanceLogItemDto(
    long Id,
    long? ScheduleId,
    int UserId,
    string EmployeeCode,
    string FullName,
    string? AvatarUrl,
    int BranchId,
    string BranchName,
    DateTime WorkDate,
    string? ShiftCode,
    string? ShiftName,
    string? ShiftStartTime,
    string? ShiftEndTime,
    DateTime CheckinTime,
    DateTime? CheckoutTime,
    int LateMinutes,
    int EarlyMinutes,
    decimal ActualWorkHours,
    string Status,
    string? Note,
    double CheckinDistance,
    double? CheckoutDistance,
    bool CheckinWifiMatched,
    bool? CheckoutWifiMatched,
    double? CheckinFaceScore,
    double? CheckoutFaceScore,
    string CheckinImageUrl,
    string? CheckoutImageUrl,
    bool BranchRequiresWifi)
{
    public static AttendanceLogItemDto From(FW.Application.Common.Interfaces.Persistence.AttendanceLogDetail d) => new(
        d.Id, d.ScheduleId, d.UserId, d.EmployeeCode, d.FullName, d.AvatarUrl, d.BranchId, d.BranchName, d.WorkDate,
        d.ShiftCode, d.ShiftName, FormatTime(d.ShiftStartTime), FormatTime(d.ShiftEndTime),
        d.CheckinTime, d.CheckoutTime, d.LateMinutes, d.EarlyMinutes, d.ActualWorkHours, d.Status, d.Note,
        d.CheckinDistance, d.CheckoutDistance, d.CheckinWifiMatched, d.IsCheckedOut ? d.CheckoutWifiMatched : null,
        d.CheckinFaceScore, d.CheckoutFaceScore, d.CheckinImageUrl, d.CheckoutImageUrl, d.BranchRequiresWifi);

    private static string? FormatTime(TimeSpan? time) => time?.ToString(@"hh\:mm");
}

public record AttendanceGeofenceDto(int Id, string Name, decimal Latitude, decimal Longitude, int Radius);

/// <summary>Chi tiết một lượt chấm công: minh chứng 3 lớp của lượt vào/ra, tâm chi nhánh, thông tin duyệt.</summary>
public record AttendanceLogDetailDto(
    AttendanceLogItemDto Log,
    AttendanceEvidenceDto CheckIn,
    AttendanceEvidenceDto? CheckOut,
    AttendanceGeofenceDto Branch,
    int? ReviewedBy,
    string? ReviewerName,
    DateTime? ReviewedAt,
    string? ReviewNote,
    bool CanReview,
    int? CheckoutAdjustedBy,
    string? CheckoutAdjusterName,
    DateTime? CheckoutAdjustedAt,
    string? CheckoutAdjustNote,
    bool CanAdjustCheckout)
{
    public static AttendanceLogDetailDto From(FW.Application.Common.Interfaces.Persistence.AttendanceLogDetail d, bool canReview, bool canAdjustCheckout = false) => new(
        AttendanceLogItemDto.From(d),
        new AttendanceEvidenceDto(d.CheckinTime, d.CheckinImageUrl, d.CheckinLat, d.CheckinLng, d.CheckinAccuracy, d.CheckinDistance,
            d.CheckinBssid, d.CheckinWifiMatched, d.CheckinFaceMatched, d.CheckinFaceScore, d.CheckinDeviceId),
        d.CheckoutTime is { } outTime
            ? new AttendanceEvidenceDto(outTime, d.CheckoutImageUrl, d.CheckoutLat, d.CheckoutLng, d.CheckoutAccuracy, d.CheckoutDistance,
                d.CheckoutBssid, d.CheckoutWifiMatched, d.CheckoutFaceMatched, d.CheckoutFaceScore, d.CheckoutDeviceId)
            : null,
        new AttendanceGeofenceDto(d.BranchId, d.BranchName, d.BranchLatitude, d.BranchLongitude, d.BranchRadius),
        d.ReviewedBy, d.ReviewerName, d.ReviewedAt, d.ReviewNote, canReview,
        d.CheckoutAdjustedBy, d.CheckoutAdjusterName, d.CheckoutAdjustedAt, d.CheckoutAdjustNote, canAdjustCheckout);
}
