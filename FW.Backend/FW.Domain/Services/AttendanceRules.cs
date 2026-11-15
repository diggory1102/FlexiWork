using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>Một ca đã xếp kèm khung ca của nó (dữ liệu đầu vào khi chọn ca để chấm công).</summary>
public record ScheduledShift(Schedule Schedule, Shift Shift)
{
    public DateTime StartAt => Shift.StartAt(Schedule.WorkDate);
    public DateTime EndAt => Shift.EndAt(Schedule.WorkDate);
}

/// <summary>
/// Quy tắc thời gian của chấm công (Task 3.2):
/// <list type="bullet">
/// <item>Check-in được từ <c>giờ bắt đầu ca − số phút mở sớm</c> tới trước <c>giờ kết thúc ca</c>; ca phải đã công bố
/// và chưa check-in. Ca nối tiếp nhau (ca gãy) chọn ca bắt đầu sớm nhất còn mở.</item>
/// <item>Check-out được tới <c>giờ kết thúc ca + số phút cho phép</c>; quá hạn thì để tác vụ nền gắn cờ quên check-out.</item>
/// <item>Giờ làm thực tế chỉ tính phần có mặt NẰM TRONG khung ca (đến sớm / về muộn không cộng thêm — làm thêm giờ đi
/// theo đơn OT), quy đổi theo tỉ lệ giờ làm/giờ có mặt của ca để trừ giờ nghỉ: có mặt trọn ca = đúng
/// <see cref="Shift.WorkingMinutes"/>.</item>
/// </list>
/// </summary>
public static class AttendanceRules
{
    public static DateTime CheckInOpensAt(ScheduledShift item, int earlyCheckInMinutes) =>
        item.StartAt.AddMinutes(-earlyCheckInMinutes);

    public static DateTime CheckOutDeadline(ScheduledShift item, int lateCheckOutMinutes) =>
        item.EndAt.AddMinutes(lateCheckOutMinutes);

    /// <summary>Ca đang trong khung giờ check-in tại thời điểm <paramref name="now"/>.</summary>
    public static bool IsCheckInOpen(ScheduledShift item, DateTime now, int earlyCheckInMinutes) =>
        item.Schedule.IsPublished &&
        item.Schedule.Status == ScheduleStatus.Scheduled &&
        now >= CheckInOpensAt(item, earlyCheckInMinutes) &&
        now < item.EndAt;

    /// <summary>Chọn ca để check-in: ca bắt đầu sớm nhất đang mở khung check-in; null nếu không có.</summary>
    public static ScheduledShift? PickCheckIn(IEnumerable<ScheduledShift> items, DateTime now, int earlyCheckInMinutes) =>
        items.Where(i => IsCheckInOpen(i, now, earlyCheckInMinutes))
             .OrderBy(i => i.StartAt)
             .ThenBy(i => i.Schedule.Id)
             .FirstOrDefault();

    /// <summary>Ca sắp tới gần nhất (chưa mở khung check-in) — để báo nhân viên "chưa tới giờ".</summary>
    public static ScheduledShift? NextUpcoming(IEnumerable<ScheduledShift> items, DateTime now, int earlyCheckInMinutes) =>
        items.Where(i => i.Schedule.IsPublished && i.Schedule.Status == ScheduleStatus.Scheduled &&
                         now < CheckInOpensAt(i, earlyCheckInMinutes))
             .OrderBy(i => i.StartAt)
             .FirstOrDefault();

    /// <summary>Giờ làm thực tế (giờ, làm tròn 2 chữ số) của lượt có mặt [checkIn, checkOut] trong ca.</summary>
    public static decimal WorkedHours(Shift shift, DateTime workDate, DateTime checkIn, DateTime checkOut)
    {
        var start = shift.StartAt(workDate);
        var end = shift.EndAt(workDate);
        var from = checkIn > start ? checkIn : start;
        var to = checkOut < end ? checkOut : end;
        if (to <= from || shift.Duration <= TimeSpan.Zero) return 0;

        var presentMinutes = (to - from).TotalMinutes;
        var workedMinutes = presentMinutes * Math.Max(shift.WorkingMinutes, 0) / shift.Duration.TotalMinutes;
        return Math.Round((decimal)workedMinutes / 60m, 2, MidpointRounding.AwayFromZero);
    }
}
