using FW.Domain.Entities;

namespace FW.UnitTests.Domain;

/// <summary>
/// Tính giờ của khung ca: độ dài, giờ làm thực tế, ca qua ngày và chồng giờ — nền tảng cho xếp lịch (Task 2.3)
/// và tính đi muộn/về sớm khi chấm công (Task 3.2).
/// </summary>
public class ShiftTests
{
    private static readonly DateTime Monday = new(2026, 11, 16);

    private static Shift NewShift(int startHour, int endHour, int breakMinutes = 0) => new()
    {
        StartTime = TimeSpan.FromHours(startHour),
        EndTime = TimeSpan.FromHours(endHour),
        BreakMinutes = breakMinutes,
        IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour))
    };

    [Fact]
    public void Day_shift_duration_and_working_minutes_exclude_break()
    {
        var office = NewShift(8, 17, breakMinutes: 60);

        Assert.False(office.IsCrossDay);
        Assert.Equal(TimeSpan.FromHours(9), office.Duration);
        Assert.Equal(480, office.WorkingMinutes);
        Assert.Equal(Monday.AddHours(8), office.StartAt(Monday));
        Assert.Equal(Monday.AddHours(17), office.EndAt(Monday));
    }

    [Fact]
    public void Night_shift_ends_on_the_next_day()
    {
        var night = NewShift(22, 6, breakMinutes: 60);

        Assert.True(night.IsCrossDay);
        Assert.Equal(TimeSpan.FromHours(8), night.Duration);
        Assert.Equal(420, night.WorkingMinutes);
        Assert.Equal(Monday.AddHours(22), night.StartAt(Monday));
        Assert.Equal(Monday.AddDays(1).AddHours(6), night.EndAt(Monday));
    }

    [Fact]
    public void Start_time_component_of_work_date_is_ignored()
    {
        var morning = NewShift(8, 12);
        Assert.Equal(Monday.AddHours(8), morning.StartAt(Monday.AddHours(15).AddMinutes(30)));
    }

    [Theory]
    [InlineData(8, 12, 10, 14, true)]   // chồng một phần
    [InlineData(8, 17, 9, 12, true)]    // ca trong ca
    [InlineData(8, 12, 12, 17, false)]  // nối tiếp nhau (ca gãy hợp lệ)
    [InlineData(8, 12, 13, 17, false)]  // cách nhau giờ nghỉ trưa
    public void Shifts_on_the_same_day_overlap_only_when_hours_intersect(int s1, int e1, int s2, int e2, bool expected)
    {
        var first = NewShift(s1, e1);
        var second = NewShift(s2, e2);

        Assert.Equal(expected, first.OverlapsWith(Monday, second, Monday));
        Assert.Equal(expected, second.OverlapsWith(Monday, first, Monday)); // đối xứng
    }

    [Fact]
    public void Night_shift_overlaps_early_shift_of_the_next_day_but_not_a_following_one()
    {
        var night = NewShift(22, 6);
        var early = NewShift(5, 12);
        var following = NewShift(6, 14);

        Assert.True(night.OverlapsWith(Monday, early, Monday.AddDays(1)));
        Assert.False(night.OverlapsWith(Monday, following, Monday.AddDays(1)));
        Assert.False(night.OverlapsWith(Monday, early, Monday)); // ca sáng cùng ngày kết thúc trước 22:00
    }
}
