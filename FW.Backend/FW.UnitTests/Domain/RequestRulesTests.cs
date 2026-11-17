using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class RequestRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0);

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    // ------------------------------------------------------------------ Nghỉ

    [Theory]
    [InlineData(17, 17, 1)]
    [InlineData(17, 19, 3)]
    [InlineData(28, 30, 3)]
    public void Leave_days_count_both_ends(int startDay, int endDay, int expected) =>
        Assert.Equal(expected, RequestRules.LeaveDays(D(11, startDay), D(11, endDay)));

    [Fact]
    public void Valid_leave_has_no_error()
    {
        Assert.Null(RequestRules.ValidateLeave(D(11, 20), D(11, 22), Today, deductsBalance: true));
        Assert.Null(RequestRules.ValidateLeave(D(11, 10), D(11, 10), Today, deductsBalance: false)); // xin bù đúng 7 ngày trước
    }

    [Fact]
    public void Leave_end_before_start_is_rejected() =>
        Assert.Contains("sau hoặc bằng", RequestRules.ValidateLeave(D(11, 22), D(11, 20), Today, false));

    [Fact]
    public void Leave_more_than_seven_days_back_is_rejected() =>
        Assert.Contains("7 ngày", RequestRules.ValidateLeave(D(11, 9), D(11, 9), Today, false));

    [Fact]
    public void Leave_longer_than_thirty_days_is_rejected()
    {
        Assert.Null(RequestRules.ValidateLeave(D(12, 1), D(12, 30), Today, false));
        Assert.Contains("30 ngày", RequestRules.ValidateLeave(D(12, 1), D(12, 31), Today, false));
    }

    [Fact]
    public void Leave_more_than_a_year_ahead_is_rejected() =>
        Assert.Contains("1 năm", RequestRules.ValidateLeave(D(11, 18, 2027), D(11, 18, 2027), Today, false));

    [Fact]
    public void Annual_leave_cannot_cross_into_next_year_but_unpaid_leave_can()
    {
        Assert.Contains("sang năm sau", RequestRules.ValidateLeave(D(12, 30), D(1, 2, 2027), Today, deductsBalance: true));
        Assert.Null(RequestRules.ValidateLeave(D(12, 30), D(1, 2, 2027), Today, deductsBalance: false));
    }

    [Fact]
    public void Available_days_subtract_pending_requests() => Assert.Equal(7.5m, RequestRules.AvailableLeaveDays(11, 3.5m));

    // ------------------------------------------------------------------ OT

    [Fact]
    public void Overtime_hours_round_to_two_decimals()
    {
        Assert.Equal(2.5m, RequestRules.OvertimeHours(Now.AddHours(8), Now.AddHours(10.5)));
        Assert.Equal(1.33m, RequestRules.OvertimeHours(Now, Now.AddMinutes(80)));
    }

    [Fact]
    public void Overtime_may_cross_midnight() =>
        Assert.Null(RequestRules.ValidateOvertime(new DateTime(2026, 11, 18, 22, 0, 0), new DateTime(2026, 11, 19, 1, 0, 0), Now));

    [Theory]
    [InlineData(0, "sau giờ bắt đầu")]
    [InlineData(-30, "sau giờ bắt đầu")]
    [InlineData(29, "tối thiểu 30 phút")]
    [InlineData(12 * 60 + 1, "tối đa 12 giờ")]
    public void Overtime_duration_must_be_between_thirty_minutes_and_twelve_hours(int minutes, string error)
    {
        var start = new DateTime(2026, 11, 18, 18, 0, 0);
        Assert.Contains(error, RequestRules.ValidateOvertime(start, start.AddMinutes(minutes), Now));
    }

    [Fact]
    public void Overtime_window_is_seven_days_back_to_sixty_days_ahead()
    {
        Assert.Null(RequestRules.ValidateOvertime(new DateTime(2026, 11, 10, 18, 0, 0), new DateTime(2026, 11, 10, 20, 0, 0), Now));
        Assert.Contains("7 ngày", RequestRules.ValidateOvertime(new DateTime(2026, 11, 9, 18, 0, 0), new DateTime(2026, 11, 9, 20, 0, 0), Now));
        Assert.Contains("60 ngày", RequestRules.ValidateOvertime(Now.AddDays(61), Now.AddDays(61).AddHours(2), Now));
    }

    [Fact]
    public void Touching_ranges_do_not_overlap()
    {
        Assert.False(RequestRules.Overlaps(Now, Now.AddHours(2), Now.AddHours(2), Now.AddHours(3)));
        Assert.True(RequestRules.Overlaps(Now, Now.AddHours(2), Now.AddHours(1), Now.AddHours(3)));
    }

    // ------------------------------------------------------------------ Đổi ca

    private static Shift NewShift(int id, int startHour, int endHour) => new()
    {
        Id = id, Name = $"Ca {startHour}-{endHour}", StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour),
        IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour))
    };

    private static Schedule NewSchedule(Shift shift, DateTime day, long id = 1, bool published = true, string status = ScheduleStatus.Scheduled) =>
        new() { Id = id, ShiftId = shift.Id, WorkDate = day, IsPublished = published, Status = status };

    [Fact]
    public void Only_published_upcoming_scheduled_shifts_are_swappable()
    {
        var morning = NewShift(1, 8, 12);
        var tomorrow = new DateTime(2026, 11, 18);
        Assert.Null(RequestRules.ValidateSwappable(NewSchedule(morning, tomorrow), morning, Now, "Ca của bạn"));
        Assert.Contains("chưa được công bố", RequestRules.ValidateSwappable(NewSchedule(morning, tomorrow, published: false), morning, Now, "Ca của bạn"));
        Assert.Contains("đã chấm công", RequestRules.ValidateSwappable(NewSchedule(morning, tomorrow, status: ScheduleStatus.Cancelled), morning, Now, "Ca của bạn"));
        // Ca sáng hôm nay bắt đầu 08:00, bây giờ 10:00
        Assert.Contains("đã bắt đầu", RequestRules.ValidateSwappable(NewSchedule(morning, Now.Date), morning, Now, "Ca của bạn"));
    }

    [Fact]
    public void Taking_over_a_shift_detects_duplicates_and_overlaps()
    {
        var morning = NewShift(1, 8, 12);
        var afternoon = NewShift(2, 12, 17);
        var lateMorning = NewShift(3, 10, 14);
        var night = NewShift(4, 22, 6);
        var day = new DateTime(2026, 11, 18);

        // Ca nối tiếp (sáng kết thúc 12:00, chiều bắt đầu 12:00) không vướng
        Assert.Null(RequestRules.FindTakeOverConflict(afternoon, day, new[] { (NewSchedule(morning, day), morning) }));
        // Trùng cùng ca cùng ngày
        Assert.NotNull(RequestRules.FindTakeOverConflict(morning, day, new[] { (NewSchedule(morning, day), morning) }));
        // Chồng giờ
        Assert.NotNull(RequestRules.FindTakeOverConflict(lateMorning, day, new[] { (NewSchedule(morning, day), morning) }));
        // Ca đêm hôm trước (22:00 → 06:00) chồng với ca bắt đầu 05:00 hôm nay
        var early = NewShift(5, 5, 9);
        Assert.NotNull(RequestRules.FindTakeOverConflict(early, day, new[] { (NewSchedule(night, day.AddDays(-1)), night) }));
        // Ca đã hủy thì không tính
        Assert.Null(RequestRules.FindTakeOverConflict(morning, day,
            new[] { (NewSchedule(morning, day, status: ScheduleStatus.Cancelled), morning) }));
    }

    // ------------------------------------------------------------------ Giải trình

    private static AttendanceLog Log(int late = 0, int early = 0, string status = AttendanceStatus.Valid, int daysAgo = 1) => new()
    {
        LateMinutes = late, EarlyMinutes = early, Status = status, WorkDate = Today.AddDays(-daysAgo).ToDateTime(TimeOnly.MinValue)
    };

    [Fact]
    public void Only_late_early_or_pending_logs_need_explanation()
    {
        Assert.Contains("không cần giải trình", RequestRules.ValidateExplanation(Log(), Today));
        Assert.Null(RequestRules.ValidateExplanation(Log(late: 12), Today));
        Assert.Null(RequestRules.ValidateExplanation(Log(early: 5), Today));
        Assert.Null(RequestRules.ValidateExplanation(Log(status: AttendanceStatus.PendingApproval), Today));
    }

    [Fact]
    public void Explanation_must_be_within_seven_days()
    {
        Assert.Null(RequestRules.ValidateExplanation(Log(late: 3, daysAgo: 7), Today));
        Assert.Contains("7 ngày", RequestRules.ValidateExplanation(Log(late: 3, daysAgo: 8), Today));
    }
}
