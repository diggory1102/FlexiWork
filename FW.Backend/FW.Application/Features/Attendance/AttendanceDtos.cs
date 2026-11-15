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
