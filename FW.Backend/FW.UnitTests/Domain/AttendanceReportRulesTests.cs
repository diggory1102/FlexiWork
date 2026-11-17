using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class AttendanceReportRulesTests
{
    private static readonly DateTime Day = new(2036, 6, 10);
    private static readonly DateTime Start = Day.AddHours(8);
    private static readonly DateTime End = Day.AddHours(17);

    private static readonly ViolationRule[] Rules =
    {
        new() { ViolationType = ViolationType.Late, MinMinutes = 1, PenaltyAmount = 20000 },
        new() { ViolationType = ViolationType.Late, MinMinutes = 15, PenaltyAmount = 50000 },
        new() { ViolationType = ViolationType.Late, MinMinutes = 60, DeductWorkUnits = 0.5m, PenaltyAmount = 50000 },
        new() { ViolationType = ViolationType.EarlyLeave, MinMinutes = 1, PenaltyAmount = 20000 }
    };

    private static ShiftDayFacts Facts(string? status = null, DateTime? checkout = null, int late = 0, int early = 0, bool onLeave = false, bool exempted = false) =>
        new(Start, End, onLeave, status, checkout, late, early, 5, 5, exempted);

    // ------------------------------------------------------------------ Tình trạng ca

    [Theory]
    [InlineData(7, 0, ShiftDayState.Upcoming)]
    [InlineData(8, 0, ShiftDayState.WaitingCheckIn)]  // đúng giờ bắt đầu
    [InlineData(16, 59, ShiftDayState.WaitingCheckIn)]
    [InlineData(17, 0, ShiftDayState.Absent)]         // đúng giờ kết thúc → coi là đã qua
    [InlineData(23, 0, ShiftDayState.Absent)]
    public void Without_a_log_the_state_follows_the_clock(int hour, int minute, ShiftDayState expected) =>
        Assert.Equal(expected, AttendanceReportRules.StateOf(Facts(), Day.AddHours(hour).AddMinutes(minute)));

    [Fact]
    public void Approved_leave_wins_over_absence_but_not_over_a_log()
    {
        Assert.Equal(ShiftDayState.OnLeave, AttendanceReportRules.StateOf(Facts(onLeave: true), Day.AddHours(20)));
        Assert.Equal(ShiftDayState.Working, AttendanceReportRules.StateOf(Facts(AttendanceStatus.Valid, onLeave: true), Day.AddHours(12)));
    }

    [Theory]
    [InlineData(AttendanceStatus.Valid)]
    [InlineData(AttendanceStatus.PendingApproval)]
    public void A_log_without_check_out_is_working_and_with_check_out_is_completed(string status)
    {
        Assert.Equal(ShiftDayState.Working, AttendanceReportRules.StateOf(Facts(status), Day.AddHours(12)));
        Assert.Equal(ShiftDayState.Completed, AttendanceReportRules.StateOf(Facts(status, checkout: End), Day.AddHours(18)));
    }

    [Fact]
    public void A_rejected_log_is_neither_attended_nor_absent()
    {
        var facts = Facts(AttendanceStatus.Invalid);
        Assert.Equal(ShiftDayState.Rejected, AttendanceReportRules.StateOf(facts, Day.AddHours(20)));
        Assert.False(facts.Attended);
        Assert.False(AttendanceReportRules.Evaluate(facts, Rules, Day.AddHours(20)).HasViolation);
    }

    [Fact]
    public void A_shift_spanning_midnight_is_still_running_after_midnight()
    {
        var night = new ShiftDayFacts(Day.AddHours(22), Day.AddDays(1).AddHours(6), false, null, null, 0, 0, 5, 5);
        Assert.Equal(ShiftDayState.WaitingCheckIn, AttendanceReportRules.StateOf(night, Day.AddDays(1).AddHours(1)));
        Assert.Equal(ShiftDayState.Absent, AttendanceReportRules.StateOf(night, Day.AddDays(1).AddHours(6)));
    }

    // ------------------------------------------------------------------ Vi phạm

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]   // trong số phút cho phép
    [InlineData(6, true)]
    [InlineData(20, true)]
    public void Late_only_counts_beyond_the_grace_minutes(int late, bool expected) =>
        Assert.Equal(expected, AttendanceReportRules.Evaluate(Facts(AttendanceStatus.Valid, End, late: late), Rules, Day.AddHours(18)).IsLate);

    [Fact]
    public void Late_uses_the_highest_matching_tier()
    {
        var result = AttendanceReportRules.Evaluate(Facts(AttendanceStatus.Valid, End, late: 70), Rules, Day.AddHours(18));

        Assert.Equal((70, 50000m, 0.5m), (result.Late.Minutes, result.Penalty, result.DeductWorkUnits));
    }

    [Fact]
    public void Early_leave_is_only_evaluated_after_check_out()
    {
        Assert.False(AttendanceReportRules.Evaluate(Facts(AttendanceStatus.Valid, checkout: null, early: 30), Rules, Day.AddHours(12)).IsEarly);

        var done = AttendanceReportRules.Evaluate(Facts(AttendanceStatus.Valid, End.AddMinutes(-30), early: 30), Rules, Day.AddHours(18));
        Assert.True(done.IsEarly);
        Assert.Equal(20000m, done.Penalty);
    }

    [Fact]
    public void An_approved_explanation_exempts_late_and_early()
    {
        var result = AttendanceReportRules.Evaluate(Facts(AttendanceStatus.Valid, End.AddMinutes(-30), late: 20, early: 30, exempted: true), Rules, Day.AddHours(18));

        Assert.False(result.HasViolation);
        Assert.Equal(0m, result.Penalty);
    }

    [Fact]
    public void A_pending_log_still_counts_as_attended_for_violations()
    {
        var result = AttendanceReportRules.Evaluate(Facts(AttendanceStatus.PendingApproval, End, late: 20), Rules, Day.AddHours(18));

        Assert.True(result.IsLate);
        Assert.Equal(50000m, result.Penalty);
    }

    [Fact]
    public void Absence_is_a_violation_but_leave_and_future_shifts_are_not()
    {
        Assert.True(AttendanceReportRules.Evaluate(Facts(), Rules, Day.AddHours(20)).IsAbsent);
        Assert.True(AttendanceReportRules.Evaluate(Facts(), Rules, Day.AddHours(20)).HasViolation);
        Assert.False(AttendanceReportRules.Evaluate(Facts(onLeave: true), Rules, Day.AddHours(20)).HasViolation);
        Assert.False(AttendanceReportRules.Evaluate(Facts(), Rules, Day.AddHours(7)).HasViolation);
        Assert.False(AttendanceReportRules.Evaluate(Facts(), Rules, Day.AddHours(12)).HasViolation); // đang trong ca, còn kịp chấm công
    }

    // ------------------------------------------------------------------ Tỷ lệ

    [Theory]
    [InlineData(1, 3, 33.3)]
    [InlineData(2, 3, 66.7)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 5, 100)]
    public void Rate_is_a_percentage_rounded_to_one_decimal(int part, int whole, double expected) =>
        Assert.Equal((decimal)expected, AttendanceReportRules.Rate(part, whole));

    [Fact]
    public void Rate_is_null_without_a_denominator() => Assert.Null(AttendanceReportRules.Rate(0, 0));
}
