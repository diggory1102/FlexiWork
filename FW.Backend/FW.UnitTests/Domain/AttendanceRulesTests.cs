using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class AttendanceRulesTests
{
    private const int Early = 60;
    private static readonly DateTime Day = new(2026, 11, 16);

    private static Shift NewShift(int startHour, int endHour, int breakMinutes = 0, int id = 1) => new()
    {
        Id = id,
        Name = $"Ca {startHour}-{endHour}",
        StartTime = TimeSpan.FromHours(startHour),
        EndTime = TimeSpan.FromHours(endHour),
        BreakMinutes = breakMinutes,
        IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour))
    };

    private static ScheduledShift Item(Shift shift, DateTime workDate, long id = 1, bool published = true, string status = ScheduleStatus.Scheduled) =>
        new(new Schedule { Id = id, ShiftId = shift.Id, WorkDate = workDate, IsPublished = published, Status = status }, shift);

    private static DateTime At(int hour, int minute = 0, int dayOffset = 0) => Day.AddDays(dayOffset).AddHours(hour).AddMinutes(minute);

    [Theory]
    [InlineData(6, 59, false)] // trước giờ mở (8:00 − 60')
    [InlineData(7, 0, true)]
    [InlineData(8, 30, true)]  // đi muộn vẫn check-in được
    [InlineData(16, 59, true)]
    [InlineData(17, 0, false)] // ca đã kết thúc
    public void Check_in_window_opens_early_and_closes_at_shift_end(int hour, int minute, bool open) =>
        Assert.Equal(open, AttendanceRules.IsCheckInOpen(Item(NewShift(8, 17), Day), At(hour, minute), Early));

    [Fact]
    public void Draft_or_already_checked_in_schedules_cannot_be_checked_in()
    {
        var shift = NewShift(8, 17);
        Assert.False(AttendanceRules.IsCheckInOpen(Item(shift, Day, published: false), At(8), Early));
        Assert.False(AttendanceRules.IsCheckInOpen(Item(shift, Day, status: ScheduleStatus.InProgress), At(8), Early));
        Assert.False(AttendanceRules.IsCheckInOpen(Item(shift, Day, status: ScheduleStatus.Completed), At(8), Early));
    }

    [Fact]
    public void Overnight_shift_of_yesterday_is_still_open_after_midnight()
    {
        var night = Item(NewShift(22, 6), Day.AddDays(-1));
        Assert.True(AttendanceRules.IsCheckInOpen(night, At(0, 30), Early));
        Assert.False(AttendanceRules.IsCheckInOpen(night, At(6), Early));
        Assert.Equal(At(6, 0), night.EndAt);
    }

    [Fact]
    public void Split_shift_picks_the_earliest_open_shift_then_the_next()
    {
        var morning = Item(NewShift(8, 12, id: 1), Day, id: 10);
        var afternoon = Item(NewShift(13, 17, id: 2), Day, id: 11);
        var items = new[] { afternoon, morning };

        // 12:00–12:59 chỉ còn ca chiều mở (ca sáng đã kết thúc lúc 12:00)
        Assert.Equal(10, AttendanceRules.PickCheckIn(items, At(7, 30), Early)!.Schedule.Id);
        Assert.Equal(10, AttendanceRules.PickCheckIn(items, At(11, 59), Early)!.Schedule.Id);
        Assert.Equal(11, AttendanceRules.PickCheckIn(items, At(12), Early)!.Schedule.Id);

        // Đã check-in ca sáng → lúc 12:10 vẫn chọn ca chiều; trước 7:00 thì chưa có ca nào
        var checkedIn = new[] { Item(morning.Shift, Day, id: 10, status: ScheduleStatus.InProgress), afternoon };
        Assert.Equal(11, AttendanceRules.PickCheckIn(checkedIn, At(12, 10), Early)!.Schedule.Id);
        Assert.Null(AttendanceRules.PickCheckIn(items, At(6, 30), Early));
        Assert.Equal(10, AttendanceRules.NextUpcoming(items, At(6, 30), Early)!.Schedule.Id);
        Assert.Null(AttendanceRules.NextUpcoming(items, At(12, 30), Early));
    }

    [Fact]
    public void Check_out_deadline_extends_past_shift_end() =>
        Assert.Equal(At(21), AttendanceRules.CheckOutDeadline(Item(NewShift(8, 17), Day), 240));

    [Theory]
    [InlineData(8, 0, 17, 0, 8.00)]  // trọn ca 9 giờ − 60' nghỉ = 8 giờ
    [InlineData(7, 0, 18, 0, 8.00)]  // đến sớm / về muộn không cộng thêm
    [InlineData(8, 30, 17, 0, 7.56)] // muộn 30': 510' có mặt × 480/540 = 453,3' ≈ 7,56 giờ
    [InlineData(8, 0, 12, 30, 4.00)] // về lúc 12:30: 270' × 480/540 = 240'
    [InlineData(17, 30, 18, 0, 0.00)] // check-in sau khi ca kết thúc
    public void Worked_hours_count_only_presence_inside_the_shift(int inH, int inM, int outH, int outM, double expected) =>
        Assert.Equal((decimal)expected, AttendanceRules.WorkedHours(NewShift(8, 17, breakMinutes: 60), Day, At(inH, inM), At(outH, outM)));

    [Fact]
    public void Worked_hours_of_an_overnight_shift_span_midnight()
    {
        var night = NewShift(22, 6, breakMinutes: 30); // 8 giờ có mặt, 7,5 giờ làm
        Assert.Equal(7.50m, AttendanceRules.WorkedHours(night, Day, At(21, 50), At(6, 5, dayOffset: 1)));
        Assert.Equal(3.75m, AttendanceRules.WorkedHours(night, Day, At(22), At(2, 0, dayOffset: 1)));
    }
}
