using FW.Application.Common.Interfaces;

namespace FW.Infrastructure.Services;

/// <summary>
/// Giờ hệ thống theo múi giờ Việt Nam (UTC+7 cố định — Việt Nam không áp dụng giờ mùa hè).
/// Dùng độ lệch cố định thay vì tra TimeZoneInfo để chạy giống nhau trên Windows, Linux và Docker.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => DateTime.SpecifyKind(DateTime.UtcNow + VietnamOffset, DateTimeKind.Unspecified);

    public DateOnly Today => DateOnly.FromDateTime(Now);
}
