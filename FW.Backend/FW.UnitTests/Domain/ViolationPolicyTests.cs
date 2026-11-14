using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

/// <summary>
/// Cách tính đi muộn / về sớm và chọn bậc xử lý vi phạm (dùng ở Task 3.2 và 5.3).
/// </summary>
public class ViolationPolicyTests
{
    private static readonly DateTime Monday = new(2026, 11, 16);

    private static readonly Shift Office = new() { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), LateGraceMinutes = 5 };
    private static readonly Shift Night = new() { StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6), IsCrossDay = true };

    private static readonly ViolationRule[] Rules =
    {
        new() { ViolationType = ViolationType.Late, MinMinutes = 1, PenaltyAmount = 20_000 },
        new() { ViolationType = ViolationType.Late, MinMinutes = 15, PenaltyAmount = 50_000 },
        new() { ViolationType = ViolationType.Late, MinMinutes = 60, DeductWorkUnits = 0.5m, PenaltyAmount = 50_000 },
        new() { ViolationType = ViolationType.EarlyLeave, MinMinutes = 1, PenaltyAmount = 10_000 },
    };

    [Theory]
    [InlineData(7, 50, 0)]   // đến sớm
    [InlineData(8, 0, 0)]    // đúng giờ
    [InlineData(8, 5, 5)]
    public void Late_minutes_are_whole_minutes_after_shift_start(int hour, int minute, int expected)
    {
        Assert.Equal(expected, ViolationPolicy.LateMinutes(Office, Monday, Monday.AddHours(hour).AddMinutes(minute)));
    }

    [Fact]
    public void Partial_minute_is_rounded_down()
    {
        Assert.Equal(5, ViolationPolicy.LateMinutes(Office, Monday, Monday.AddHours(8).AddMinutes(5).AddSeconds(59)));
    }

    [Fact]
    public void Early_leave_of_night_shift_is_measured_against_next_morning()
    {
        var checkOut = Monday.AddDays(1).AddHours(5).AddMinutes(30);
        Assert.Equal(30, ViolationPolicy.EarlyLeaveMinutes(Night, Monday, checkOut));
        Assert.Equal(0, ViolationPolicy.EarlyLeaveMinutes(Night, Monday, Monday.AddDays(1).AddHours(6).AddMinutes(10)));
    }

    [Theory]
    [InlineData(0, false, 0, 0)]       // đúng giờ
    [InlineData(5, false, 0, 0)]       // trong 5 phút cho phép
    [InlineData(6, true, 0, 20_000)]   // vượt cho phép → tính cả 6 phút, bậc "từ 1 phút"
    [InlineData(15, true, 0, 50_000)]  // đúng mốc bậc 2
    [InlineData(59, true, 0, 50_000)]
    [InlineData(90, true, 0.5, 50_000)] // bậc cao nhất: trừ nửa công (không cộng dồn các bậc dưới)
    public void Highest_matching_tier_applies_once_grace_is_exceeded(int minutes, bool isViolation, double deduct, int penalty)
    {
        var outcome = ViolationPolicy.Evaluate(Rules, ViolationType.Late, minutes, graceMinutes: 5);

        Assert.Equal(minutes, outcome.Minutes);
        Assert.Equal(isViolation, outcome.IsViolation);
        Assert.Equal((decimal)deduct, outcome.DeductWorkUnits);
        Assert.Equal(penalty, outcome.PenaltyAmount);
    }

    [Fact]
    public void Rules_of_other_violation_type_are_ignored()
    {
        var outcome = ViolationPolicy.Evaluate(Rules, ViolationType.EarlyLeave, 90, graceMinutes: 0);
        Assert.Equal(10_000, outcome.PenaltyAmount);
        Assert.Equal(0, outcome.DeductWorkUnits);
    }

    [Fact]
    public void Violation_without_matching_tier_has_no_penalty()
    {
        var outcome = ViolationPolicy.Evaluate(Array.Empty<ViolationRule>(), ViolationType.Late, 30, graceMinutes: 5);
        Assert.True(outcome.IsViolation);
        Assert.Null(outcome.Rule);
        Assert.Equal(0, outcome.PenaltyAmount);
    }
}
