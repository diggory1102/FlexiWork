using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Nhật ký chấm công của một ca đã xếp (bảng attendance_logs; mỗi ca tối đa một bản ghi). Lưu đủ minh chứng của
/// cả lượt vào và lượt ra cho 3 lớp xác minh: (1) GPS — tọa độ, sai số, khoảng cách tới tâm chi nhánh;
/// (2) Wi-Fi — BSSID điện thoại bắt được và có khớp Wi-Fi nội bộ không; (3) khuôn mặt — ảnh selfie và độ tương đồng
/// với khuôn mặt mẫu. Kèm mã thiết bị để phát hiện một điện thoại chấm công cho nhiều người.
/// </summary>
public class AttendanceLog : BaseEntity<long>, IAuditableEntity
{
    /// <summary>Ca được chấm công; null nếu lịch đó đã bị xóa khỏi hệ thống.</summary>
    public long? ScheduleId { get; set; }
    public int UserId { get; set; }

    /// <summary>Chi nhánh nơi làm ca (dùng để kiểm tra Geofence / Wi-Fi và tra cứu theo chi nhánh).</summary>
    public int BranchId { get; set; }

    /// <summary>Ngày công = ngày bắt đầu ca (ca qua đêm vẫn tính vào ngày bắt đầu).</summary>
    public DateTime WorkDate { get; set; }

    // ----- Check-in -----
    public DateTime CheckinTime { get; set; }
    public string CheckinImageUrl { get; set; } = string.Empty;
    public decimal CheckinLat { get; set; }
    public decimal CheckinLng { get; set; }

    /// <summary>Sai số vị trí điện thoại báo cáo (m); null nếu không có.</summary>
    public double? CheckinAccuracy { get; set; }

    /// <summary>Khoảng cách tới tâm chi nhánh (m).</summary>
    public double CheckinDistance { get; set; }
    public string? CheckinBssid { get; set; }
    public bool CheckinWifiMatched { get; set; }
    public bool CheckinFaceMatched { get; set; }

    /// <summary>Độ tương đồng cosine với khuôn mặt mẫu (0–1).</summary>
    public double? CheckinFaceScore { get; set; }
    public string? CheckinDeviceId { get; set; }

    // ----- Check-out -----
    public DateTime? CheckoutTime { get; set; }
    public string? CheckoutImageUrl { get; set; }
    public decimal? CheckoutLat { get; set; }
    public decimal? CheckoutLng { get; set; }
    public double? CheckoutAccuracy { get; set; }
    public double? CheckoutDistance { get; set; }
    public string? CheckoutBssid { get; set; }
    public bool CheckoutWifiMatched { get; set; }
    public bool CheckoutFaceMatched { get; set; }
    public double? CheckoutFaceScore { get; set; }
    public string? CheckoutDeviceId { get; set; }

    // ----- Đối soát -----
    /// <summary>Số phút đi muộn so với giờ bắt đầu ca (0 nếu đúng giờ).</summary>
    public int LateMinutes { get; set; }

    /// <summary>Số phút về sớm so với giờ kết thúc ca (0 nếu đúng giờ).</summary>
    public int EarlyMinutes { get; set; }

    /// <summary>Giờ làm thực tế (đã trừ giờ nghỉ), tính khi check-out.</summary>
    public decimal ActualWorkHours { get; set; }

    /// <summary>Giá trị thuộc <see cref="AttendanceStatus"/>.</summary>
    public string Status { get; set; } = AttendanceStatus.Valid;

    /// <summary>Lý do hệ thống ghi lại (VD: lớp Wi-Fi không đạt → chờ duyệt).</summary>
    public string? Note { get; set; }

    /// <summary>Người duyệt bản ghi chờ duyệt (Quản lý / HR); null nếu chưa duyệt hoặc tài khoản đã bị xóa.</summary>
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Đã check-out chưa (thuộc tính tính toán, không ánh xạ xuống CSDL).</summary>
    public bool IsCheckedOut => CheckoutTime.HasValue;
}
