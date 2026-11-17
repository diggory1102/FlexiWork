using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>Tình trạng của một ca đã xếp tại một thời điểm (dùng cho dashboard quân số và báo cáo vi phạm).</summary>
public enum ShiftDayState
{
    /// <summary>Ca chưa bắt đầu.</summary>
    Upcoming,

    /// <summary>Ca đã bắt đầu, chưa kết thúc mà chưa có lượt chấm công.</summary>
    WaitingCheckIn,

    /// <summary>Ca đã kết thúc mà không có lượt chấm công và không nghỉ phép.</summary>
    Absent,

    /// <summary>Ca nằm trong kỳ nghỉ đã duyệt.</summary>
    OnLeave,

    /// <summary>Đã check-in (hợp lệ hoặc chờ duyệt), chưa check-out.</summary>
    Working,

    /// <summary>Đã check-in và check-out.</summary>
    Completed,

    /// <summary>Lượt chấm công bị từ chối — không tính là có mặt, cũng không tính vắng mặt.</summary>
    Rejected
}

/// <summary>Dữ kiện của một ca đã xếp kèm lượt chấm công (nếu có).</summary>
/// <param name="ViolationExempted">Đơn giải trình cho lượt chấm công này đã được duyệt — miễn đi muộn / về sớm.</param>
public sealed record ShiftDayFacts(
    DateTime StartAt,
    DateTime EndAt,
    bool OnLeave,
    string? LogStatus,
    DateTime? CheckoutTime,
    int LateMinutes,
    int EarlyMinutes,
    int LateGraceMinutes,
    int EarlyLeaveGraceMinutes,
    bool ViolationExempted = false)
{
    public bool HasLog => LogStatus is not null;

    /// <summary>Có mặt: lượt chấm công hợp lệ hoặc đang chờ duyệt.</summary>
    public bool Attended => LogStatus is AttendanceStatus.Valid or AttendanceStatus.PendingApproval;
}

/// <summary>Kết quả đánh giá vi phạm của một ca (không có vi phạm thì các <see cref="ViolationOutcome"/> có <c>IsViolation = false</c>).</summary>
public sealed record ShiftViolations(ShiftDayState State, ViolationOutcome Late, ViolationOutcome Early)
{
    public bool IsLate => Late.IsViolation;
    public bool IsEarly => Early.IsViolation;
    public bool IsAbsent => State == ShiftDayState.Absent;
    public bool HasViolation => IsLate || IsEarly || IsAbsent;
    public decimal Penalty => Late.PenaltyAmount + Early.PenaltyAmount;
    public decimal DeductWorkUnits => Late.DeductWorkUnits + Early.DeductWorkUnits;
}

/// <summary>
/// Quy tắc phân loại ca cho dashboard / báo cáo (Task 5.6) — hàm thuần, kiểm thử độc lập với CSDL.
/// Có lượt chấm công: bị từ chối → <see cref="ShiftDayState.Rejected"/>; còn lại chưa check-out → Working, đã check-out → Completed.
/// Không có lượt chấm công: nghỉ phép → OnLeave; ca đã kết thúc → Absent; ca đã bắt đầu → WaitingCheckIn; chưa bắt đầu → Upcoming.
/// Đi muộn / về sớm theo <see cref="ViolationPolicy"/> (vượt số phút cho phép của ca; đơn giải trình đã duyệt thì miễn).
/// </summary>
public static class AttendanceReportRules
{
    public static ShiftDayState StateOf(ShiftDayFacts facts, DateTime now)
    {
        if (facts.HasLog)
        {
            if (facts.LogStatus == AttendanceStatus.Invalid) return ShiftDayState.Rejected;
            return facts.CheckoutTime is null ? ShiftDayState.Working : ShiftDayState.Completed;
        }

        if (facts.OnLeave) return ShiftDayState.OnLeave;
        if (facts.EndAt <= now) return ShiftDayState.Absent;
        return facts.StartAt <= now ? ShiftDayState.WaitingCheckIn : ShiftDayState.Upcoming;
    }

    public static ShiftViolations Evaluate(ShiftDayFacts facts, IReadOnlyCollection<ViolationRule> rules, DateTime now)
    {
        var state = StateOf(facts, now);
        var none = new ViolationOutcome(0, false, null);
        if (!facts.Attended || facts.ViolationExempted) return new ShiftViolations(state, none, none);

        var late = ViolationPolicy.Evaluate(rules, ViolationType.Late, facts.LateMinutes, facts.LateGraceMinutes);
        // Về sớm chỉ xét khi đã check-out
        var early = facts.CheckoutTime is null
            ? none
            : ViolationPolicy.Evaluate(rules, ViolationType.EarlyLeave, facts.EarlyMinutes, facts.EarlyLeaveGraceMinutes);
        return new ShiftViolations(state, late, early);
    }

    /// <summary>Tỷ lệ phần trăm làm tròn 1 chữ số; null khi không có mẫu số.</summary>
    public static decimal? Rate(int part, int whole) => whole <= 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
}
