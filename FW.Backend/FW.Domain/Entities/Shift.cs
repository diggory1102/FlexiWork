using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Khung ca làm việc (bảng shifts). Ca có <see cref="BranchId"/> = null dùng chung cho mọi chi nhánh;
/// ngược lại chỉ chi nhánh đó xếp được. Ca gãy được tách thành hai ca riêng trong cùng một ngày.
/// </summary>
public class Shift : BaseAuditableEntity
{
    public string ShiftCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Chi nhánh sở hữu ca; null = ca dùng chung toàn hệ thống.</summary>
    public int? BranchId { get; set; }

    /// <summary>Giờ bắt đầu trong ngày (cột TIME).</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Giờ kết thúc; nhỏ hơn hoặc bằng giờ bắt đầu nghĩa là ca kết thúc vào ngày hôm sau.</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>Thời gian nghỉ giữa ca (phút), không tính vào giờ làm.</summary>
    public int BreakMinutes { get; set; }

    /// <summary>Hệ số công quy đổi (1.00 = một công, 0.50 = nửa công...).</summary>
    public decimal WorkUnits { get; set; } = 1.00m;

    /// <summary>Ca đêm — được tính phụ cấp ca đêm ở bảng lương.</summary>
    public bool IsNightShift { get; set; }

    /// <summary>Ca qua ngày (kết thúc vào hôm sau). Luôn bằng <see cref="SpansMidnight"/>(StartTime, EndTime).</summary>
    public bool IsCrossDay { get; set; }

    /// <summary>Số phút được phép check-in trễ mà chưa tính là đi muộn.</summary>
    public int LateGraceMinutes { get; set; } = 5;

    /// <summary>Số phút được phép check-out sớm mà chưa tính là về sớm.</summary>
    public int EarlyLeaveGraceMinutes { get; set; } = 5;

    /// <summary>Ca kết thúc vào ngày hôm sau khi giờ kết thúc không lớn hơn giờ bắt đầu (VD 22:00 → 06:00).</summary>
    public static bool SpansMidnight(TimeSpan startTime, TimeSpan endTime) => endTime <= startTime;

    /// <summary>Tổng độ dài ca, tính cả giờ nghỉ.</summary>
    public TimeSpan Duration => SpansMidnight(StartTime, EndTime)
        ? EndTime + TimeSpan.FromDays(1) - StartTime
        : EndTime - StartTime;

    /// <summary>Số phút làm việc thực tế = độ dài ca − giờ nghỉ.</summary>
    public int WorkingMinutes => (int)Duration.TotalMinutes - BreakMinutes;

    /// <summary>Thời điểm bắt đầu ca khi được xếp vào ngày <paramref name="workDate"/>.</summary>
    public DateTime StartAt(DateTime workDate) => workDate.Date + StartTime;

    /// <summary>Thời điểm kết thúc ca khi được xếp vào ngày <paramref name="workDate"/> (sang hôm sau nếu là ca qua ngày).</summary>
    public DateTime EndAt(DateTime workDate) => StartAt(workDate) + Duration;

    /// <summary>
    /// Hai ca (mỗi ca xếp vào một ngày) có chồng giờ nhau không. Hai ca nối tiếp (ca trước kết thúc đúng lúc ca sau bắt đầu)
    /// không bị coi là chồng giờ.
    /// </summary>
    public bool OverlapsWith(DateTime workDate, Shift other, DateTime otherWorkDate) =>
        StartAt(workDate) < other.EndAt(otherWorkDate) && other.StartAt(otherWorkDate) < EndAt(workDate);
}
