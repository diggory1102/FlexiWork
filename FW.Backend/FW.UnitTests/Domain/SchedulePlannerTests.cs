using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

/// <summary>
/// Phát hiện trùng ca / chồng giờ khi xếp lịch (Task 2.3).
/// </summary>
public class SchedulePlannerTests
{
    private static readonly DateTime Monday = new(2026, 11, 16);

    private static readonly Shift Morning = NewShift(1, 8, 12);
    private static readonly Shift Afternoon = NewShift(2, 12, 17);
    private static readonly Shift Office = NewShift(3, 8, 17);
    private static readonly Shift Night = NewShift(4, 22, 6);
    private static readonly Shift Early = NewShift(5, 5, 12);

    private static Shift NewShift(int id, int start, int end) => new()
    {
        Id = id,
        ShiftCode = $"S{id}",
        StartTime = TimeSpan.FromHours(start),
        EndTime = TimeSpan.FromHours(end),
        IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(start), TimeSpan.FromHours(end))
    };

    private static PlannedShift P(int userId, Shift shift, int day) => new(userId, shift, Monday.AddDays(day));

    [Fact]
    public void Shifts_on_different_days_or_for_different_people_do_not_conflict()
    {
        var plan = SchedulePlanner.Plan(
            new[] { P(1, Office, 0), P(1, Office, 1), P(2, Office, 0) },
            new[] { P(3, Office, 0) });

        Assert.Equal(3, plan.Accepted.Count);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void Same_shift_same_day_as_existing_is_a_duplicate()
    {
        var plan = SchedulePlanner.Plan(new[] { P(1, Morning, 0) }, new[] { P(1, Morning, 0) });

        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal(ScheduleConflictKind.Duplicate, conflict.Kind);
        Assert.True(conflict.WithIsExisting);
        Assert.Empty(plan.Accepted);
    }

    [Fact]
    public void Overlapping_hours_on_the_same_day_are_rejected()
    {
        var plan = SchedulePlanner.Plan(new[] { P(1, Office, 0) }, new[] { P(1, Morning, 0) });

        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal(ScheduleConflictKind.Overlap, conflict.Kind);
        Assert.Equal(Morning, conflict.With.Shift);
    }

    [Fact]
    public void Night_shift_of_previous_day_blocks_early_shift_next_morning()
    {
        var plan = SchedulePlanner.Plan(new[] { P(1, Early, 1) }, new[] { P(1, Night, 0) });

        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal(ScheduleConflictKind.Overlap, conflict.Kind);
        Assert.Equal(Monday, conflict.With.Date);
    }

    [Fact]
    public void Split_shift_made_of_back_to_back_shifts_is_allowed()
    {
        var plan = SchedulePlanner.Plan(new[] { P(1, Morning, 0), P(1, Afternoon, 0) }, Array.Empty<PlannedShift>());

        Assert.Equal(2, plan.Accepted.Count);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void Conflicts_inside_the_same_request_reject_the_later_item_only()
    {
        var plan = SchedulePlanner.Plan(
            new[] { P(1, Morning, 0), P(1, Morning, 0), P(1, Office, 0), P(1, Afternoon, 1) },
            Array.Empty<PlannedShift>());

        Assert.Equal(new[] { P(1, Morning, 0), P(1, Afternoon, 1) }, plan.Accepted);
        Assert.Equal(2, plan.Conflicts.Count);
        Assert.Equal(ScheduleConflictKind.Duplicate, plan.Conflicts[0].Kind);
        Assert.Equal(ScheduleConflictKind.Overlap, plan.Conflicts[1].Kind);
        Assert.All(plan.Conflicts, c => Assert.False(c.WithIsExisting));
    }

    [Fact]
    public void Time_component_of_work_date_is_ignored()
    {
        var item = new PlannedShift(1, Morning, Monday.AddHours(15));
        var plan = SchedulePlanner.Plan(new[] { item }, new[] { P(1, Morning, 0) });

        Assert.Equal(ScheduleConflictKind.Duplicate, Assert.Single(plan.Conflicts).Kind);
    }
}
