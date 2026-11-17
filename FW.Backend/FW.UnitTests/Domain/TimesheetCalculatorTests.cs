using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class TimesheetCalculatorTests
{
    private static readonly DateTime Now = new(2026, 12, 5, 9, 0, 0); // sau khi tháng 11 kết thúc
    private const int LateCheckOut = 240;
    private static long _id = 100;

    private static readonly ViolationRule[] Rules =
    {
        new() { ViolationType = ViolationType.Late, MinMinutes = 1, DeductWorkUnits = 0, PenaltyAmount = 20000 },
        new() { ViolationType = ViolationType.Late, MinMinutes = 15, DeductWorkUnits = 0, PenaltyAmount = 50000 },
        new() { ViolationType = ViolationType.Late, MinMinutes = 60, DeductWorkUnits = 0.5m, PenaltyAmount = 50000 },
        new() { ViolationType = ViolationType.EarlyLeave, MinMinutes = 1, DeductWorkUnits = 0, PenaltyAmount = 20000 },
        new() { ViolationType = ViolationType.EarlyLeave, MinMinutes = 15, DeductWorkUnits = 0, PenaltyAmount = 50000 },
        new() { ViolationType = ViolationType.EarlyLeave, MinMinutes = 60, DeductWorkUnits = 0.5m, PenaltyAmount = 50000 },
    };

    private static Shift NewShift(int startHour, int endHour, decimal units = 1m, bool night = false, int id = 1) => new()
    {
        Id = id, Name = $"Ca {startHour}-{endHour}", StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour),
        WorkUnits = units, IsNightShift = night, LateGraceMinutes = 5, EarlyLeaveGraceMinutes = 5,
        IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour))
    };

    private static Schedule NewSchedule(Shift shift, int day, string status = ScheduleStatus.Scheduled, bool published = true) => new()
    {
        Id = ++_id, ShiftId = shift.Id, WorkDate = new DateTime(2026, 11, day), IsPublished = published, Status = status
    };

    private static AttendanceLog Done(Schedule s, int late = 0, int early = 0, decimal hours = 8, string status = AttendanceStatus.Valid) => new()
    {
        Id = ++_id, ScheduleId = s.Id, WorkDate = s.WorkDate, CheckinTime = s.WorkDate.AddHours(8).AddMinutes(late), CheckoutTime = s.WorkDate.AddHours(17),
        LateMinutes = late, EarlyMinutes = early, ActualWorkHours = hours, Status = status
    };

    private static ShiftEntry Entry(Shift shift, Schedule schedule, AttendanceLog? log = null, bool exempted = false, LeaveKind? leave = null) =>
        new(schedule, shift, log, exempted, leave);

    private static TimesheetResult Calc(IEnumerable<ShiftEntry> entries, IEnumerable<OvertimeEntry>? overtime = null, DateTime? now = null) =>
        TimesheetCalculator.Calculate(entries, overtime ?? Array.Empty<OvertimeEntry>(), Rules, now ?? Now, LateCheckOut);

    // ------------------------------------------------------------------ đã làm & vi phạm

    [Fact]
    public void A_fully_attended_shift_counts_its_work_units_and_hours()
    {
        var shift = NewShift(8, 17);
        var schedule = NewSchedule(shift, 3);
        var result = Calc(new[] { Entry(shift, schedule, Done(schedule, hours: 7.5m)) });

        Assert.Equal((1, 1, 0), (result.Totals.ScheduledShifts, result.Totals.WorkedShifts, result.Totals.AbsentShifts));
        Assert.Equal((1m, 7.5m, 0, 0), (result.Totals.ActualWorkDays, result.Totals.ActualWorkHours, result.Totals.TotalLateMinutes, result.Totals.ViolationCount));
        Assert.Equal(ShiftOutcome.Worked, Assert.Single(result.Lines).Outcome);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 1.0, 0)]        // đúng giờ
    [InlineData(5, 0, 0, 0, 0, 1.0, 0)]        // muộn trong mức cho phép (5') → không tính vi phạm
    [InlineData(7, 0, 7, 0, 1, 1.0, 20000)]    // vượt mức cho phép → tính TOÀN BỘ 7 phút, phạt bậc 1
    [InlineData(20, 0, 20, 0, 1, 1.0, 50000)]  // bậc 15'
    [InlineData(70, 0, 70, 0, 1, 0.5, 50000)]  // bậc 60': trừ nửa công
    [InlineData(0, 10, 0, 10, 1, 1.0, 20000)]  // về sớm 10'
    [InlineData(20, 10, 20, 10, 1, 1.0, 70000)] // vừa muộn vừa sớm cùng một ca: một lượt vi phạm, phạt cộng hai bậc
    [InlineData(70, 70, 70, 70, 1, 0.0, 100000)] // trừ hết công của ca (không âm)
    public void Violations_follow_the_tiered_rules(int late, int early, int lateOut, int earlyOut, int count, double units, double penalty)
    {
        var shift = NewShift(8, 17);
        var schedule = NewSchedule(shift, 4);
        var totals = Calc(new[] { Entry(shift, schedule, Done(schedule, late, early)) }).Totals;

        Assert.Equal((lateOut, earlyOut, count), (totals.TotalLateMinutes, totals.TotalEarlyMinutes, totals.ViolationCount));
        Assert.Equal(((decimal)units, (decimal)penalty), (totals.ActualWorkDays, totals.PenaltyAmount));
        Assert.Equal(1m - (decimal)units, totals.DeductedWorkDays);
    }

    [Fact]
    public void Deduction_never_exceeds_the_work_units_of_a_short_shift()
    {
        var shift = NewShift(8, 12, units: 0.25m);
        var schedule = NewSchedule(shift, 5);
        var result = Calc(new[] { Entry(shift, schedule, Done(schedule, late: 90)) }).Totals;

        Assert.Equal((0m, 0.25m), (result.ActualWorkDays, result.DeductedWorkDays));
    }

    [Fact]
    public void An_approved_explanation_exempts_the_violations_of_that_log()
    {
        var shift = NewShift(8, 17);
        var schedule = NewSchedule(shift, 6);
        var result = Calc(new[] { Entry(shift, schedule, Done(schedule, late: 70, early: 20), exempted: true) });

        Assert.Equal((1m, 0, 0, 0, 0m), (result.Totals.ActualWorkDays, result.Totals.TotalLateMinutes, result.Totals.TotalEarlyMinutes,
            result.Totals.ViolationCount, result.Totals.PenaltyAmount));
        Assert.True(Assert.Single(result.Lines).Exempted);
    }

    // ------------------------------------------------------------------ lượt chấm công chưa tính

    [Fact]
    public void Pending_rejected_and_missing_checkout_logs_are_not_counted()
    {
        var shift = NewShift(8, 17);
        var pending = NewSchedule(shift, 7);
        var rejected = NewSchedule(shift, 8);
        var missing = NewSchedule(shift, 9);
        var noCheckout = Done(missing);
        noCheckout.CheckoutTime = null;

        var result = Calc(new[]
        {
            Entry(shift, pending, Done(pending, status: AttendanceStatus.PendingApproval)),
            Entry(shift, rejected, Done(rejected, status: AttendanceStatus.Invalid)),
            Entry(shift, missing, noCheckout)
        });

        Assert.Equal((0, 1, 1, 1), (result.Totals.WorkedShifts, result.Totals.PendingLogs, result.Totals.RejectedLogs, result.Totals.MissingCheckouts));
        Assert.Equal(0m, result.Totals.ActualWorkDays);
    }

    [Fact]
    public void A_shift_still_in_progress_is_not_flagged_until_the_checkout_deadline_passes()
    {
        var shift = NewShift(8, 17);
        var schedule = NewSchedule(shift, 10);
        var inProgress = Done(schedule);
        inProgress.CheckoutTime = null;
        var entry = Entry(shift, schedule, inProgress);

        // 18:00 cùng ngày: ca đã hết nhưng còn trong hạn check-out (17:00 + 240')
        Assert.Equal(ShiftOutcome.Upcoming, Calc(new[] { entry }, now: schedule.WorkDate.AddHours(18)).Lines.Single().Outcome);
        Assert.Equal(ShiftOutcome.Upcoming, Calc(new[] { entry }, now: schedule.WorkDate.AddHours(21)).Lines.Single().Outcome);
        Assert.Equal(ShiftOutcome.MissingCheckout, Calc(new[] { entry }, now: schedule.WorkDate.AddHours(21).AddMinutes(1)).Lines.Single().Outcome);
    }

    // ------------------------------------------------------------------ vắng mặt & nghỉ phép

    [Fact]
    public void A_finished_shift_without_attendance_is_absent_but_a_future_one_is_not()
    {
        var shift = NewShift(8, 17);
        var past = NewSchedule(shift, 11);
        var future = new Schedule { Id = ++_id, ShiftId = shift.Id, WorkDate = Now.Date.AddDays(1), IsPublished = true };
        var endingNow = new Schedule { Id = ++_id, ShiftId = shift.Id, WorkDate = Now.Date.AddDays(-1), IsPublished = true };

        var result = Calc(new[] { Entry(shift, past), Entry(shift, future), Entry(shift, endingNow) });

        Assert.Equal((3, 0, 2), (result.Totals.ScheduledShifts, result.Totals.WorkedShifts, result.Totals.AbsentShifts));
        Assert.Equal(ShiftOutcome.Upcoming, result.Lines.Single(l => l.Schedule.Id == future.Id).Outcome);
    }

    [Fact]
    public void Leave_shifts_add_their_work_units_to_paid_or_unpaid_leave()
    {
        var full = NewShift(8, 17);
        var half = NewShift(8, 12, units: 0.5m, id: 2);
        var result = Calc(new[]
        {
            Entry(full, NewSchedule(full, 12, ScheduleStatus.OnLeave), leave: LeaveKind.Paid),
            Entry(half, NewSchedule(half, 13, ScheduleStatus.OnLeave), leave: LeaveKind.Paid),
            Entry(full, NewSchedule(full, 14, ScheduleStatus.OnLeave), leave: LeaveKind.Unpaid),
            Entry(full, NewSchedule(full, 15, ScheduleStatus.OnLeave)),                 // nghỉ nhưng không rõ đơn → không lương
            Entry(full, NewSchedule(full, 16), leave: LeaveKind.Paid)                   // nằm trong kỳ nghỉ đã duyệt dù trạng thái ca chưa cập nhật
        });

        Assert.Equal((2.5m, 2m, 0, 0), (result.Totals.PaidLeaveDays, result.Totals.UnpaidLeaveDays, result.Totals.WorkedShifts, result.Totals.AbsentShifts));
    }

    [Fact]
    public void Attendance_wins_over_a_leave_mark()
    {
        var shift = NewShift(8, 17);
        var schedule = NewSchedule(shift, 17, ScheduleStatus.OnLeave);
        var result = Calc(new[] { Entry(shift, schedule, Done(schedule), leave: LeaveKind.Paid) });

        Assert.Equal((1, 0m), (result.Totals.WorkedShifts, result.Totals.PaidLeaveDays));
    }

    // ------------------------------------------------------------------ chọn ca, ca đêm, ca gãy, OT

    [Fact]
    public void Drafts_and_cancelled_shifts_are_ignored()
    {
        var shift = NewShift(8, 17);
        var result = Calc(new[]
        {
            Entry(shift, NewSchedule(shift, 18, published: false)),
            Entry(shift, NewSchedule(shift, 19, ScheduleStatus.Cancelled)),
            Entry(shift, NewSchedule(shift, 20))
        });

        Assert.Equal((1, 1), (result.Totals.ScheduledShifts, result.Totals.AbsentShifts));
    }

    [Fact]
    public void Night_shifts_are_counted_when_worked()
    {
        var day = NewShift(8, 17);
        var night = NewShift(22, 6, night: true, id: 2);
        var nightSchedule = NewSchedule(night, 21);
        var absentNight = NewSchedule(night, 22);
        var result = Calc(new[]
        {
            Entry(day, NewSchedule(day, 21), Done(NewSchedule(day, 21))),
            Entry(night, nightSchedule, Done(nightSchedule)),
            Entry(night, absentNight)
        });

        Assert.Equal(1, result.Totals.NightShifts); // ca đêm vắng mặt không tính
    }

    [Fact]
    public void Split_shift_days_need_a_gap_between_worked_shifts()
    {
        var morning = NewShift(8, 12, id: 1);
        var afternoon = NewShift(12, 17, id: 2);   // nối tiếp ca sáng
        var evening = NewShift(18, 22, id: 3);      // cách ca chiều 1 giờ
        Schedule S(Shift shift, int day) => NewSchedule(shift, day);
        TimesheetResult Of(params (Shift Shift, int Day)[] items) =>
            Calc(items.Select(i => { var s = S(i.Shift, i.Day); return Entry(i.Shift, s, Done(s)); }));

        Assert.Equal(0, Of((morning, 23), (afternoon, 23)).Totals.SplitShiftDays);            // liên tục → không phải ca gãy
        Assert.Equal(1, Of((morning, 24), (evening, 24)).Totals.SplitShiftDays);              // sáng + tối
        Assert.Equal(1, Of((morning, 25), (afternoon, 25), (evening, 25)).Totals.SplitShiftDays); // 3 ca, có một khoảng nghỉ
        Assert.Equal(2, Of((morning, 26), (evening, 26), (morning, 27), (evening, 27)).Totals.SplitShiftDays);
        Assert.Equal(0, Of((morning, 28)).Totals.SplitShiftDays);
        // Ca vắng mặt không làm ngày thành ca gãy
        var absent = S(evening, 29);
        var worked = S(morning, 29);
        Assert.Equal(0, Calc(new[] { Entry(morning, worked, Done(worked)), Entry(evening, absent) }).Totals.SplitShiftDays);
    }

    [Fact]
    public void Overtime_hours_are_summed_and_rounded_to_two_decimals()
    {
        var overtime = new[]
        {
            new OvertimeEntry(new DateTime(2026, 11, 3, 17, 0, 0), new DateTime(2026, 11, 3, 19, 30, 0), 2.5m),
            new OvertimeEntry(new DateTime(2026, 11, 9, 18, 0, 0), new DateTime(2026, 11, 9, 19, 20, 0), 1.33m),
        };
        Assert.Equal(3.83m, Calc(Array.Empty<ShiftEntry>(), overtime).Totals.OvertimeHours);
    }

    [Fact]
    public void Totals_add_up_over_a_whole_month_and_lines_are_in_date_order()
    {
        var shift = NewShift(8, 17);
        var entries = new List<ShiftEntry>();
        foreach (var day in new[] { 5, 2, 9 })
        {
            var schedule = NewSchedule(shift, day);
            entries.Add(Entry(shift, schedule, Done(schedule, late: day == 9 ? 20 : 0, hours: 8)));
        }

        var result = Calc(entries);
        Assert.Equal(new[] { 2, 5, 9 }, result.Lines.Select(l => l.Schedule.WorkDate.Day));
        Assert.Equal((3m, 24m, 20, 1, 50000m), (result.Totals.ActualWorkDays, result.Totals.ActualWorkHours, result.Totals.TotalLateMinutes,
            result.Totals.ViolationCount, result.Totals.PenaltyAmount));
    }

    [Fact]
    public void An_empty_month_is_all_zeros()
    {
        var totals = Calc(Array.Empty<ShiftEntry>()).Totals;
        Assert.Equal(0, totals.ScheduledShifts + totals.WorkedShifts + totals.AbsentShifts + totals.NightShifts + totals.SplitShiftDays);
        Assert.Equal(0m, totals.ActualWorkDays + totals.OvertimeHours + totals.PaidLeaveDays + totals.PenaltyAmount);
    }

    [Fact]
    public void Differs_detects_changes_in_the_figures_that_matter()
    {
        var shift = NewShift(8, 17);
        var schedule = NewSchedule(shift, 3);
        var before = Calc(new[] { Entry(shift, schedule, Done(schedule, status: AttendanceStatus.PendingApproval)) }).Totals;
        var after = Calc(new[] { Entry(shift, schedule, Done(schedule)) }).Totals;

        Assert.False(TimesheetCalculator.Differs(before, before));
        Assert.True(TimesheetCalculator.Differs(before, after)); // lượt chờ duyệt vừa được duyệt sau khi đã chốt
    }
}
