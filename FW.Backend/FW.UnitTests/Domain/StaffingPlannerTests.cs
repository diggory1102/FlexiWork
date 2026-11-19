using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

/// <summary>
/// Định biên ca: mỗi ô (ca, ngày) nhận thêm người cho tới số tối đa, tính cả người đã xếp trước đó và người cùng yêu cầu.
/// </summary>
public class StaffingPlannerTests
{
    private static readonly DateTime Monday = new(2026, 11, 16);

    private static Shift NewShift(int id, int? max) => new()
    {
        Id = id,
        ShiftCode = $"S{id}",
        StartTime = TimeSpan.FromHours(8),
        EndTime = TimeSpan.FromHours(12),
        MaxStaff = max
    };

    private static PlannedShift P(int userId, Shift shift, int day) => new(userId, shift, Monday.AddDays(day));

    private static (int, DateTime) Slot(Shift shift, int day) => (shift.Id, Monday.AddDays(day));

    [Fact]
    public void A_shift_without_a_maximum_is_never_limited()
    {
        var open = NewShift(1, null);

        var plan = StaffingPlanner.Plan(Enumerable.Range(1, 50).Select(u => P(u, open, 0)), Enumerable.Repeat(Slot(open, 0), 100));

        Assert.Equal(50, plan.Allowed.Count);
        Assert.Empty(plan.Rejected);
    }

    [Fact]
    public void People_are_accepted_until_the_maximum_counting_those_already_staffed()
    {
        var morning = NewShift(1, 3);

        var plan = StaffingPlanner.Plan(new[] { P(10, morning, 0), P(11, morning, 0), P(12, morning, 0) }, new[] { Slot(morning, 0) });

        Assert.Equal(new[] { 10, 11 }, plan.Allowed.Select(p => p.UserId)); // 1 người có sẵn + 2 = đủ 3
        var rejected = Assert.Single(plan.Rejected);
        Assert.Equal(12, rejected.Item.UserId);
        Assert.Equal((3, 3), (rejected.Current, rejected.Max));
    }

    [Fact]
    public void The_limit_is_per_shift_and_per_day()
    {
        var morning = NewShift(1, 1);
        var evening = NewShift(2, 1);

        var plan = StaffingPlanner.Plan(
            new[] { P(10, morning, 0), P(11, morning, 1), P(12, evening, 0), P(13, morning, 0) },
            Array.Empty<(int, DateTime)>());

        Assert.Equal(new[] { 10, 11, 12 }, plan.Allowed.Select(p => p.UserId)); // ngày / ca khác không dùng chung chỗ
        Assert.Equal(13, Assert.Single(plan.Rejected).Item.UserId);
    }

    [Fact]
    public void An_overfull_shift_after_the_maximum_was_lowered_only_blocks_new_people()
    {
        var morning = NewShift(1, 2); // đã có 4 người từ trước khi hạ tối đa xuống 2

        var plan = StaffingPlanner.Plan(new[] { P(10, morning, 0) }, Enumerable.Repeat(Slot(morning, 0), 4));

        Assert.Empty(plan.Allowed);
        var rejected = Assert.Single(plan.Rejected);
        Assert.Equal((4, 2), (rejected.Current, rejected.Max));
    }

    [Fact]
    public void Time_of_day_in_the_slot_date_is_ignored()
    {
        var morning = NewShift(1, 1);

        var plan = StaffingPlanner.Plan(new[] { P(10, morning, 0) }, new[] { (morning.Id, Monday.AddHours(8)) });

        Assert.Empty(plan.Allowed);
        Assert.Single(plan.Rejected);
    }
}
