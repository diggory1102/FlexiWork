namespace FW.Application.Common.Interfaces;

/// <summary>
/// Nguồn thời gian duy nhất của hệ thống. Toàn bộ nghiệp vụ (ca làm, chấm công, bảng công)
/// dùng giờ Việt Nam (UTC+7, không có giờ mùa hè) — khớp với time_zone của phiên kết nối MySQL.
/// Inject interface này thay vì gọi DateTime.Now để dễ kiểm thử.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    DateTime Now { get; }

    /// <summary>Ngày hiện tại theo giờ Việt Nam.</summary>
    DateOnly Today { get; }

    DateTime UtcNow { get; }
}
