using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

/// <summary>
/// Định biên hiệu lực của một ca trong một ngày: ngoại lệ đúng ngày > ngoại lệ theo thứ > mặc định của ca; quy tắc tìm thấy thay thế cả
/// tối thiểu lẫn tối đa; ngoại lệ chỉ đổi số người cho ô của nó.
/// </summary>
public class ShiftStaffingTests
{
    private static readonly DateTime Friday = new(2026, 12, 4);
    private static readonly DateTime Saturday = new(2026, 12, 5);
    private static readonly DateTime Sunday = new(2026, 12, 6);

    private static Shift Evening(int? min = 2, int? max = 3) => new() { Id = 7, ShiftCode = "TOI", MinStaff = min, MaxStaff = max };

    private static ShiftStaffingRule Rule(string scopeKey, int? min, int? max, string? note = null) =>
        new() { Id = 1, BranchId = 1, ShiftId = 7, ScopeKey = scopeKey, MinStaff = min, MaxStaff = max, Note = note };

    [Fact]
    public void Without_rules_the_default_of_the_shift_applies()
    {
        var limits = ShiftStaffing.Resolve(Evening(), Array.Empty<ShiftStaffingRule>(), Friday);

        Assert.Equal((2, 3, StaffingSource.Default, (string?)null), (limits.Min, limits.Max, limits.Source, limits.Note));
        Assert.True(limits.IsLimited);
    }

    [Fact]
    public void A_date_rule_changes_only_that_day()
    {
        var rules = new[] { Rule(ShiftStaffing.DateKey(Saturday), 5, 6, "Tiệc công ty ABC") };

        var party = ShiftStaffing.Resolve(Evening(), rules, Saturday);
        Assert.Equal((5, 6, StaffingSource.Date, "Tiệc công ty ABC"), (party.Min, party.Max, party.Source, party.Note));

        Assert.Equal(StaffingSource.Default, ShiftStaffing.Resolve(Evening(), rules, Friday).Source);
        Assert.Equal(StaffingSource.Default, ShiftStaffing.Resolve(Evening(), rules, Sunday).Source);
        Assert.Equal(3, ShiftStaffing.Resolve(Evening(), rules, Sunday).Max);
    }

    [Fact]
    public void A_date_rule_beats_a_weekday_rule_which_beats_the_default()
    {
        var rules = new[]
        {
            Rule(ShiftStaffing.WeekdayKey(6), 4, 4),                       // mọi thứ Bảy
            Rule(ShiftStaffing.DateKey(Saturday), 8, 9)                    // riêng ngày 05/12
        };

        Assert.Equal(9, ShiftStaffing.Resolve(Evening(), rules, Saturday).Max);                      // ngày cụ thể thắng
        var nextSaturday = ShiftStaffing.Resolve(Evening(), rules, Saturday.AddDays(7));
        Assert.Equal((4, StaffingSource.Weekday), (nextSaturday.Max, nextSaturday.Source));          // thứ Bảy khác: theo thứ
        Assert.Equal(StaffingSource.Default, ShiftStaffing.Resolve(Evening(), rules, Friday).Source); // thứ khác: mặc định
    }

    [Fact]
    public void A_rule_replaces_both_limits_and_an_empty_rule_lifts_the_limit_for_that_day()
    {
        var onlyMax = ShiftStaffing.Resolve(Evening(min: 2, max: 3), new[] { Rule(ShiftStaffing.DateKey(Friday), null, 10) }, Friday);
        Assert.Equal((null, 10), (onlyMax.Min, onlyMax.Max)); // không trộn: tối thiểu mặc định (2) không còn hiệu lực

        var lifted = ShiftStaffing.Resolve(Evening(), new[] { Rule(ShiftStaffing.DateKey(Friday), null, null) }, Friday);
        Assert.False(lifted.IsLimited);
        Assert.Equal(StaffingSource.Date, lifted.Source);
    }

    [Fact]
    public void A_shift_without_defaults_can_still_be_limited_by_a_rule()
    {
        var plain = new Shift { Id = 7 };

        Assert.False(ShiftStaffing.Resolve(plain, Array.Empty<ShiftStaffingRule>(), Friday).IsLimited);
        Assert.Equal(2, ShiftStaffing.Resolve(plain, new[] { Rule(ShiftStaffing.WeekdayKey(5), 1, 2) }, Friday).Max); // 04/12/2026 là thứ Sáu
    }

    [Theory]
    [InlineData("2026-11-30", 1)] // Thứ Hai
    [InlineData("2026-12-04", 5)] // Thứ Sáu
    [InlineData("2026-12-05", 6)] // Thứ Bảy
    [InlineData("2026-12-06", 7)] // Chủ nhật = 7 (không phải 0)
    public void Weekdays_use_the_iso_numbering_and_keys_have_a_stable_format(string date, int iso)
    {
        var day = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(iso, ShiftStaffing.IsoDayOfWeek(day));
        Assert.Equal($"dow:{iso}", ShiftStaffing.WeekdayKey(iso));
        Assert.Equal($"date:{date}", ShiftStaffing.DateKey(day.AddHours(13)));
    }

    [Fact]
    public void The_planner_uses_the_effective_maximum_when_given_one()
    {
        var shift = Evening(min: null, max: 1);
        var items = new[] { new PlannedShift(10, shift, Saturday), new PlannedShift(11, shift, Saturday), new PlannedShift(12, shift, Friday) };
        var rules = new[] { Rule(ShiftStaffing.DateKey(Saturday), null, 2) };

        var plan = StaffingPlanner.Plan(items, Array.Empty<(int, DateTime)>(), p => ShiftStaffing.Resolve(p.Shift, rules, p.Date).Max);

        Assert.Equal(new[] { 10, 11, 12 }, plan.Allowed.Select(p => p.UserId)); // thứ Bảy nới lên 2 người; thứ Sáu vẫn 1
        Assert.Empty(plan.Rejected);

        var third = StaffingPlanner.Plan(items.Append(new PlannedShift(13, shift, Saturday)), Array.Empty<(int, DateTime)>(),
            p => ShiftStaffing.Resolve(p.Shift, rules, p.Date).Max);
        var rejected = Assert.Single(third.Rejected);
        Assert.Equal((13, 2, 2), (rejected.Item.UserId, rejected.Current, rejected.Max)); // báo mức hiệu lực (2), không phải mặc định (1)
    }
}
