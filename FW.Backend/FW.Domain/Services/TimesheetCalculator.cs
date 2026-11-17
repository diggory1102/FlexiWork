using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>Nghỉ theo đơn đã duyệt: có lương hay không lương.</summary>
public enum LeaveKind
{
    Paid,
    Unpaid
}

/// <summary>Kết quả của một ca khi tính bảng công.</summary>
public enum ShiftOutcome
{
    /// <summary>Đã check-in và check-out hợp lệ — tính công.</summary>
    Worked,

    /// <summary>Ca đã qua mà không có lượt chấm công.</summary>
    Absent,

    /// <summary>Nghỉ theo đơn đã duyệt (xem <see cref="TimesheetLine.Leave"/>).</summary>
    Leave,

    /// <summary>Lượt chấm công còn chờ duyệt — chưa tính, chặn xác nhận.</summary>
    Pending,

    /// <summary>Lượt chấm công bị từ chối — không tính.</summary>
    Rejected,

    /// <summary>Có check-in nhưng quá hạn check-out mà chưa check-out — không tính, cần xử lý.</summary>
    MissingCheckout,

    /// <summary>Chưa tới lúc chốt: ca chưa kết thúc, hoặc đang trong ca còn check-out được — chưa tính.</summary>
    Upcoming
}

/// <summary>Một ca đã xếp kèm dữ liệu liên quan để tính công.</summary>
/// <param name="Log">Lượt chấm công của ca (nếu có).</param>
/// <param name="ViolationExempted">Đơn giải trình cho lượt chấm công này đã được duyệt — miễn xử lý đi muộn / về sớm.</param>
/// <param name="Leave">Ca nằm trong khoảng nghỉ của một đơn đã duyệt.</param>
public sealed record ShiftEntry(Schedule Schedule, Shift Shift, AttendanceLog? Log = null, bool ViolationExempted = false, LeaveKind? Leave = null);

/// <summary>Một đơn làm thêm giờ đã duyệt.</summary>
public sealed record OvertimeEntry(DateTime StartAt, DateTime EndAt, decimal Hours);

/// <summary>Kết quả tính của một ca (chi tiết theo ngày cho Quản lý đối soát).</summary>
public sealed record TimesheetLine(
    Schedule Schedule,
    Shift Shift,
    ShiftOutcome Outcome,
    LeaveKind? Leave,
    decimal WorkUnits,
    decimal WorkHours,
    int LateMinutes,
    int EarlyMinutes,
    decimal DeductedUnits,
    decimal Penalty,
    bool Exempted,
    long? LogId,
    DateTime? CheckIn,
    DateTime? CheckOut);

public sealed record TimesheetTotals(
    int ScheduledShifts,
    int WorkedShifts,
    int AbsentShifts,
    int PendingLogs,
    int RejectedLogs,
    int MissingCheckouts,
    decimal ActualWorkDays,
    decimal ActualWorkHours,
    int TotalLateMinutes,
    int TotalEarlyMinutes,
    int ViolationCount,
    decimal DeductedWorkDays,
    decimal PenaltyAmount,
    decimal OvertimeHours,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    int NightShifts,
    int SplitShiftDays);

public sealed record TimesheetResult(TimesheetTotals Totals, IReadOnlyList<TimesheetLine> Lines);

/// <summary>
/// Thuật toán tổng hợp bảng công tháng (Task 5.3, UC 8.2) — hàm thuần, nhận dữ liệu đã nạp sẵn nên kiểm thử độc lập với CSDL.
/// <list type="number">
/// <item>Chỉ xét ca đã công bố và chưa hủy. Mỗi ca có đúng một kết quả (<see cref="ShiftOutcome"/>).</item>
/// <item><b>Có lượt chấm công:</b> bị từ chối → không tính; chờ duyệt → chưa tính (chặn xác nhận); hợp lệ nhưng chưa check-out: còn hạn
/// check-out thì "chưa tới lúc chốt", quá hạn thì "thiếu check-out" (không tính); hợp lệ + đã check-out → <b>đã làm</b>.</item>
/// <item><b>Đã làm:</b> công = hệ số công của ca − công bị trừ theo bậc vi phạm (đi muộn + về sớm, tối thiểu 0). Vi phạm tính bằng
/// <see cref="ViolationPolicy"/> (vượt mức cho phép thì tính toàn bộ số phút). Đơn giải trình đã duyệt → miễn mọi xử lý của lượt đó.</item>
/// <item><b>Không có lượt chấm công:</b> nằm trong kỳ nghỉ đã duyệt → nghỉ (hệ số công của ca cộng vào nghỉ có / không lương); ca đã kết thúc
/// → vắng mặt; ca chưa kết thúc → chưa tới lúc chốt.</item>
/// <item>Ca đêm = ca đã làm có cờ ca đêm. <b>Ngày ca gãy</b> = ngày có từ 2 ca đã làm trở lên mà giữa chúng có khoảng nghỉ (ca nối tiếp không tính).</item>
/// <item>Giờ OT = tổng giờ các đơn OT đã duyệt trong tháng. Lương: không tính ở đây (Task 5.5).</item>
/// </list>
/// </summary>
public static class TimesheetCalculator
{
    public static TimesheetResult Calculate(
        IEnumerable<ShiftEntry> entries,
        IEnumerable<OvertimeEntry> overtime,
        IReadOnlyCollection<ViolationRule> rules,
        DateTime now,
        int lateCheckOutMinutes)
    {
        var lines = entries
            .Where(e => e.Schedule.IsPublished && e.Schedule.Status != ScheduleStatus.Cancelled)
            .OrderBy(e => e.Schedule.WorkDate).ThenBy(e => e.Shift.StartTime).ThenBy(e => e.Schedule.Id)
            .Select(e => Evaluate(e, rules, now, lateCheckOutMinutes))
            .ToList();

        var worked = lines.Where(l => l.Outcome == ShiftOutcome.Worked).ToList();
        var violating = worked.Where(l => l.LateMinutes > 0 || l.EarlyMinutes > 0).ToList();
        var leave = lines.Where(l => l.Outcome == ShiftOutcome.Leave).ToList();

        var totals = new TimesheetTotals(
            ScheduledShifts: lines.Count,
            WorkedShifts: worked.Count,
            AbsentShifts: Count(lines, ShiftOutcome.Absent),
            PendingLogs: Count(lines, ShiftOutcome.Pending),
            RejectedLogs: Count(lines, ShiftOutcome.Rejected),
            MissingCheckouts: Count(lines, ShiftOutcome.MissingCheckout),
            ActualWorkDays: Round(worked.Sum(l => l.WorkUnits)),
            ActualWorkHours: Round(worked.Sum(l => l.WorkHours)),
            TotalLateMinutes: worked.Sum(l => l.LateMinutes),
            TotalEarlyMinutes: worked.Sum(l => l.EarlyMinutes),
            ViolationCount: violating.Count,
            DeductedWorkDays: Round(worked.Sum(l => l.DeductedUnits)),
            PenaltyAmount: worked.Sum(l => l.Penalty),
            OvertimeHours: Round(overtime.Sum(o => o.Hours)),
            PaidLeaveDays: Round(leave.Where(l => l.Leave == LeaveKind.Paid).Sum(l => l.Shift.WorkUnits)),
            UnpaidLeaveDays: Round(leave.Where(l => l.Leave == LeaveKind.Unpaid).Sum(l => l.Shift.WorkUnits)),
            NightShifts: worked.Count(l => l.Shift.IsNightShift),
            SplitShiftDays: CountSplitDays(worked));

        return new TimesheetResult(totals, lines);
    }

    /// <summary>
    /// Số liệu chính có khác nhau không (dùng phát hiện bảng đã xác nhận / khóa nhưng dữ liệu nguồn đã thay đổi sau đó).
    /// </summary>
    public static bool Differs(TimesheetTotals a, TimesheetTotals b) =>
        a.WorkedShifts != b.WorkedShifts || a.AbsentShifts != b.AbsentShifts || a.PendingLogs != b.PendingLogs ||
        a.RejectedLogs != b.RejectedLogs || a.MissingCheckouts != b.MissingCheckouts || a.ActualWorkDays != b.ActualWorkDays ||
        a.ActualWorkHours != b.ActualWorkHours || a.TotalLateMinutes != b.TotalLateMinutes || a.TotalEarlyMinutes != b.TotalEarlyMinutes ||
        a.PenaltyAmount != b.PenaltyAmount || a.OvertimeHours != b.OvertimeHours || a.PaidLeaveDays != b.PaidLeaveDays ||
        a.UnpaidLeaveDays != b.UnpaidLeaveDays;

    private static TimesheetLine Evaluate(ShiftEntry e, IReadOnlyCollection<ViolationRule> rules, DateTime now, int lateCheckOutMinutes)
    {
        var (schedule, shift, log) = (e.Schedule, e.Shift, e.Log);
        var end = shift.EndAt(schedule.WorkDate);

        TimesheetLine Plain(ShiftOutcome outcome, LeaveKind? leave = null) => new(
            schedule, shift, outcome, leave, 0, 0, 0, 0, 0, 0, e.ViolationExempted, log?.Id, log?.CheckinTime, log?.CheckoutTime);

        if (log is null)
        {
            if (schedule.Status == ScheduleStatus.OnLeave || e.Leave is not null)
            {
                // Đã đánh dấu nghỉ nhưng không rõ đơn nào (hiếm) → coi là nghỉ không lương, không tự cộng thêm tiền
                return Plain(ShiftOutcome.Leave, e.Leave ?? LeaveKind.Unpaid);
            }
            return Plain(end <= now ? ShiftOutcome.Absent : ShiftOutcome.Upcoming);
        }

        if (log.Status == AttendanceStatus.Invalid) return Plain(ShiftOutcome.Rejected);
        if (log.Status == AttendanceStatus.PendingApproval) return Plain(ShiftOutcome.Pending);

        if (log.CheckoutTime is null)
        {
            return Plain(now <= end.AddMinutes(lateCheckOutMinutes) ? ShiftOutcome.Upcoming : ShiftOutcome.MissingCheckout);
        }

        // Đã làm: trừ công và phạt theo bậc vi phạm (trừ khi đơn giải trình đã được duyệt)
        int lateMinutes = 0, earlyMinutes = 0;
        decimal deduct = 0, penalty = 0;
        if (!e.ViolationExempted)
        {
            var late = ViolationPolicy.Evaluate(rules, ViolationType.Late, log.LateMinutes, shift.LateGraceMinutes);
            var early = ViolationPolicy.Evaluate(rules, ViolationType.EarlyLeave, log.EarlyMinutes, shift.EarlyLeaveGraceMinutes);
            lateMinutes = late.IsViolation ? late.Minutes : 0;
            earlyMinutes = early.IsViolation ? early.Minutes : 0;
            deduct = Math.Min(shift.WorkUnits, (late.IsViolation ? late.DeductWorkUnits : 0) + (early.IsViolation ? early.DeductWorkUnits : 0));
            penalty = (late.IsViolation ? late.PenaltyAmount : 0) + (early.IsViolation ? early.PenaltyAmount : 0);
        }

        return new TimesheetLine(schedule, shift, ShiftOutcome.Worked, null, shift.WorkUnits - deduct, log.ActualWorkHours, lateMinutes, earlyMinutes,
            deduct, penalty, e.ViolationExempted, log.Id, log.CheckinTime, log.CheckoutTime);
    }

    private static int CountSplitDays(IEnumerable<TimesheetLine> worked) =>
        worked.GroupBy(l => l.Schedule.WorkDate.Date).Count(day =>
        {
            var ordered = day.OrderBy(l => l.Shift.StartAt(l.Schedule.WorkDate)).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previousEnd = ordered.Take(i).Max(l => l.Shift.EndAt(l.Schedule.WorkDate));
                if (ordered[i].Shift.StartAt(ordered[i].Schedule.WorkDate) > previousEnd) return true; // có khoảng nghỉ giữa hai ca
            }
            return false;
        });

    private static int Count(IEnumerable<TimesheetLine> lines, ShiftOutcome outcome) => lines.Count(l => l.Outcome == outcome);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
