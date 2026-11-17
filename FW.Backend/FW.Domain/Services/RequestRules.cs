using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>
/// Quy tắc tạo đơn từ (Task 4.1) — hàm thuần để kiểm thử độc lập với CSDL. Các hàm Validate* trả về lý do không hợp lệ,
/// null nếu hợp lệ.
/// <list type="bullet">
/// <item>Nghỉ: tính theo ngày (gồm cả 2 đầu), tối đa 30 ngày / đơn; được xin bù tối đa 7 ngày trước (ốm đột xuất),
/// xin trước tối đa 1 năm; đơn trừ phép năm không vắt sang năm sau.</item>
/// <item>OT: từ 30 phút tới 12 giờ, có thể qua nửa đêm; đăng ký bù tối đa 7 ngày trước, trước tối đa 60 ngày.</item>
/// <item>Đổi ca: chỉ ca đã công bố, chưa bắt đầu, chưa chấm công; người nhận ca không bị trùng / chồng giờ.</item>
/// <item>Giải trình: lượt chấm công có đi muộn, về sớm hoặc đang chờ duyệt, trong vòng 7 ngày.</item>
/// </list>
/// </summary>
public static class RequestRules
{
    public const int MaxLeaveDays = 30;
    public const int MaxBackDays = 7;
    public const int MaxLeaveAdvanceDays = 365;
    public const int MaxOvertimeAdvanceDays = 60;
    public const int MinOvertimeMinutes = 30;
    public const int MaxOvertimeHours = 12;
    public const int MaxReasonLength = 1000;
    public const int MaxNoteLength = 255;

    // ------------------------------------------------------------------ Nghỉ

    /// <summary>Số ngày nghỉ, tính cả ngày đầu và ngày cuối.</summary>
    public static decimal LeaveDays(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    public static string? ValidateLeave(DateOnly start, DateOnly end, DateOnly today, bool deductsBalance)
    {
        if (end < start) return "Ngày kết thúc nghỉ phải sau hoặc bằng ngày bắt đầu.";
        if (start < today.AddDays(-MaxBackDays)) return $"Chỉ được tạo đơn nghỉ cho những ngày trong vòng {MaxBackDays} ngày trở lại đây.";
        if (end > today.AddDays(MaxLeaveAdvanceDays)) return "Chỉ được xin nghỉ trước tối đa 1 năm.";
        if (LeaveDays(start, end) > MaxLeaveDays) return $"Mỗi đơn nghỉ tối đa {MaxLeaveDays} ngày — nghỉ dài hơn thì tách thành nhiều đơn.";
        if (deductsBalance && start.Year != end.Year)
        {
            return "Đơn nghỉ trừ phép năm không được kéo dài sang năm sau — tách thành 2 đơn theo từng năm.";
        }
        return null;
    }

    /// <summary>Số ngày phép còn xin được = còn lại − tổng ngày của các đơn trừ phép đang chờ duyệt.</summary>
    public static decimal AvailableLeaveDays(decimal remainingDays, decimal pendingDays) => remainingDays - pendingDays;

    // ------------------------------------------------------------------ Làm thêm giờ

    public static decimal OvertimeHours(DateTime startAt, DateTime endAt) =>
        Math.Round((decimal)(endAt - startAt).TotalMinutes / 60m, 2);

    public static string? ValidateOvertime(DateTime startAt, DateTime endAt, DateTime now)
    {
        if (endAt <= startAt) return "Giờ kết thúc OT phải sau giờ bắt đầu.";
        var minutes = (endAt - startAt).TotalMinutes;
        if (minutes < MinOvertimeMinutes) return $"Mỗi lần OT tối thiểu {MinOvertimeMinutes} phút.";
        if (minutes > MaxOvertimeHours * 60) return $"Mỗi lần OT tối đa {MaxOvertimeHours} giờ.";
        if (startAt < now.Date.AddDays(-MaxBackDays)) return $"Chỉ được đăng ký OT cho những ngày trong vòng {MaxBackDays} ngày trở lại đây.";
        if (startAt > now.AddDays(MaxOvertimeAdvanceDays)) return $"Chỉ được đăng ký OT trước tối đa {MaxOvertimeAdvanceDays} ngày.";
        return null;
    }

    /// <summary>Hai khoảng thời gian giao nhau (chạm đầu mút không tính là giao).</summary>
    public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) => aStart < bEnd && bStart < aEnd;

    // ------------------------------------------------------------------ Đổi ca

    /// <summary>Ca đem đổi phải đã công bố, chưa bị hủy / chưa chấm công và chưa tới giờ bắt đầu.</summary>
    /// <param name="owner">"Ca của bạn" / "Ca của đồng nghiệp" — để ghép vào thông báo.</param>
    public static string? ValidateSwappable(Schedule schedule, Shift shift, DateTime now, string owner)
    {
        if (!schedule.IsPublished) return $"{owner} chưa được công bố.";
        if (schedule.Status != ScheduleStatus.Scheduled) return $"{owner} đã chấm công, đã hủy hoặc đã kết thúc — không đổi được.";
        if (shift.StartAt(schedule.WorkDate) <= now) return $"{owner} đã bắt đầu — chỉ đổi được ca chưa tới giờ làm.";
        return null;
    }

    /// <summary>
    /// Một người nhận thêm ca <paramref name="incoming"/> vào ngày <paramref name="workDate"/>: ca đang có (đã bỏ ca sẽ nhường đi)
    /// không được trùng (cùng ca, cùng ngày) hay chồng giờ với ca nhận.
    /// </summary>
    /// <returns>Ca bị vướng; null nếu nhận được.</returns>
    public static (Schedule Schedule, Shift Shift)? FindTakeOverConflict(
        Shift incoming, DateTime workDate, IEnumerable<(Schedule Schedule, Shift Shift)> existing) =>
        existing
            .Where(e => e.Schedule.Status != ScheduleStatus.Cancelled)
            .Select(e => ((Schedule Schedule, Shift Shift)?)e)
            .FirstOrDefault(e =>
                (e!.Value.Schedule.ShiftId == incoming.Id && e.Value.Schedule.WorkDate.Date == workDate.Date) ||
                incoming.OverlapsWith(workDate, e.Value.Shift, e.Value.Schedule.WorkDate));

    // ------------------------------------------------------------------ Giải trình

    /// <summary>Lượt chấm công có điều cần giải trình: đi muộn, về sớm hoặc đang chờ duyệt.</summary>
    public static bool NeedsExplanation(AttendanceLog log) =>
        log.LateMinutes > 0 || log.EarlyMinutes > 0 || log.Status == AttendanceStatus.PendingApproval;

    public static string? ValidateExplanation(AttendanceLog log, DateOnly today)
    {
        if (!NeedsExplanation(log)) return "Lượt chấm công này đúng giờ và hợp lệ — không cần giải trình.";
        if (DateOnly.FromDateTime(log.WorkDate) < today.AddDays(-MaxBackDays))
        {
            return $"Chỉ được giải trình lượt chấm công trong vòng {MaxBackDays} ngày.";
        }
        return null;
    }
}
