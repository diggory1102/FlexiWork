namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình chấm công (section "Attendance").
/// </summary>
public class AttendanceSettings
{
    public const string SectionName = "Attendance";

    /// <summary>Được check-in sớm tối đa bao nhiêu phút trước giờ bắt đầu ca.</summary>
    public int EarlyCheckInMinutes { get; set; } = 60;

    /// <summary>Được check-out muộn tối đa bao nhiêu phút sau giờ kết thúc ca; quá hạn coi như quên check-out.</summary>
    public int LateCheckOutMinutes { get; set; } = 240;

    /// <summary>Sai số GPS tối đa (m) điện thoại báo cáo — lớn hơn thì yêu cầu ra chỗ thoáng / bật định vị chính xác.</summary>
    public double MaxGpsAccuracyMeters { get; set; } = 100;
}
