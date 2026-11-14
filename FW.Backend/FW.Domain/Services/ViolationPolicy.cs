using FW.Domain.Entities;

namespace FW.Domain.Services;

/// <summary>Kết quả đánh giá một lượt vi phạm giờ giấc.</summary>
/// <param name="Minutes">Số phút muộn / sớm thực tế (0 nếu đúng giờ).</param>
/// <param name="IsViolation">Vượt số phút cho phép của ca hay không.</param>
/// <param name="Rule">Bậc xử lý được áp dụng; null nếu đúng giờ hoặc chưa tới bậc thấp nhất.</param>
public record ViolationOutcome(int Minutes, bool IsViolation, ViolationRule? Rule)
{
    public decimal DeductWorkUnits => Rule?.DeductWorkUnits ?? 0;
    public decimal PenaltyAmount => Rule?.PenaltyAmount ?? 0;
}

/// <summary>
/// Quy tắc tính đi muộn / về sớm (dùng khi chấm công — Task 3.2 và tổng hợp bảng công — Task 5.3):
/// 1. Số phút vi phạm làm tròn xuống theo phút (check-in 08:05:59 với ca 08:00 = muộn 5 phút).
/// 2. Không vượt số phút cho phép của ca (<see cref="Shift.LateGraceMinutes"/>) thì không tính vi phạm.
/// 3. Đã vượt thì tính TOÀN BỘ số phút (không trừ phần cho phép) và áp dụng bậc có MinMinutes lớn nhất
///    không vượt quá số phút đó.
/// </summary>
public static class ViolationPolicy
{
    public static int LateMinutes(Shift shift, DateTime workDate, DateTime checkInTime) =>
        WholeMinutes(checkInTime - shift.StartAt(workDate));

    public static int EarlyLeaveMinutes(Shift shift, DateTime workDate, DateTime checkOutTime) =>
        WholeMinutes(shift.EndAt(workDate) - checkOutTime);

    public static ViolationRule? FindRule(IEnumerable<ViolationRule> rules, string violationType, int minutes) =>
        rules.Where(r => r.ViolationType == violationType && r.MinMinutes <= minutes)
             .MaxBy(r => r.MinMinutes);

    public static ViolationOutcome Evaluate(IEnumerable<ViolationRule> rules, string violationType, int minutes, int graceMinutes)
    {
        if (minutes <= graceMinutes) return new ViolationOutcome(minutes, false, null);
        return new ViolationOutcome(minutes, true, FindRule(rules, violationType, minutes));
    }

    private static int WholeMinutes(TimeSpan span) => span <= TimeSpan.Zero ? 0 : (int)Math.Floor(span.TotalMinutes);
}
