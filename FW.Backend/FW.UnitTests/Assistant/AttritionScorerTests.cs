using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;

namespace FW.UnitTests.Assistant;

public class AttritionScorerTests
{
    // Chấm vào Thứ Hai 16/11/2026: cửa sổ gần = 02/11–15/11, nền = 8 tuần 07/09–01/11
    private static readonly DateOnly AsOf = new(2026, 11, 16);
    private static readonly DateOnly RecentStart = AsOf.AddDays(-14);
    private static readonly DateOnly BaselineStart = RecentStart.AddDays(-56);

    private static List<ShiftRecord> Week(DateOnly weekStart, int late = 0, int lateMin = 0, int absent = 0, int early = 0)
    {
        var list = new List<ShiftRecord>();
        for (var i = 0; i < 6; i++)
        {
            var date = weekStart.AddDays(i);
            if (i < absent) list.Add(new ShiftRecord(date, false, true, 0, 0));
            else if (i < absent + late) list.Add(new ShiftRecord(date, true, false, lateMin, 0));
            else if (i < absent + late + early) list.Add(new ShiftRecord(date, true, false, 0, 10));
            else list.Add(new ShiftRecord(date, true, false, 0, 0));
        }
        return list;
    }

    private static List<ShiftRecord> Weeks(DateOnly start, int count, Func<int, List<ShiftRecord>> week)
    {
        var all = new List<ShiftRecord>();
        for (var w = 0; w < count; w++) all.AddRange(week(w) is { } shifts ? Shift(start.AddDays(7 * w), shifts) : new());
        return all;

        static List<ShiftRecord> Shift(DateOnly weekStart, List<ShiftRecord> template) =>
            template.Select((s, i) => s with { Date = weekStart.AddDays(i) }).ToList();
    }

    private static EmployeeActivity Employee(
        List<ShiftRecord> shifts,
        List<RequestRecord>? requests = null,
        List<AvailabilityRecord>? availability = null) =>
        new(1, "Nhân viên A", 1, shifts, requests ?? new(), availability);

    private static List<ShiftRecord> History(Func<int, List<ShiftRecord>> baselineWeek, Func<int, List<ShiftRecord>> recentWeek)
    {
        var all = Weeks(BaselineStart, 8, baselineWeek);
        all.AddRange(Weeks(RecentStart, 2, recentWeek));
        return all;
    }

    private static List<AvailabilityRecord> Availability(int baselineSlots, int recentSlots)
    {
        var list = new List<AvailabilityRecord>();
        for (var w = 0; w < 8; w++) list.Add(new AvailabilityRecord(BaselineStart.AddDays(7 * w), baselineSlots));
        for (var w = 0; w < 2; w++) list.Add(new AvailabilityRecord(RecentStart.AddDays(7 * w), recentSlots));
        return list;
    }

    [Fact]
    public void Stable_employee_scores_low_with_no_reasons()
    {
        var shifts = History(_ => Week(BaselineStart, late: 1, lateMin: 5), _ => Week(RecentStart, late: 1, lateMin: 5));
        var result = AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions());

        Assert.True(result.DataSufficient);
        Assert.Equal(RiskLevel.Low, result.Level);
        Assert.True(result.Score < 5, $"score={result.Score}");
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Habitually_late_employee_is_not_flagged_because_only_the_change_counts()
    {
        var shifts = History(_ => Week(BaselineStart, late: 4, lateMin: 12), _ => Week(RecentStart, late: 4, lateMin: 12));
        var result = AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions());

        Assert.Equal(0, result.Score);
        Assert.Equal(RiskLevel.Low, result.Level);
    }

    [Fact]
    public void Employee_whose_behaviour_deteriorates_on_every_signal_is_high_risk_with_reasons()
    {
        var shifts = History(
            _ => Week(BaselineStart, late: 1, lateMin: 5),
            _ => Week(RecentStart, late: 4, lateMin: 20, absent: 1, early: 1));
        var requests = new List<RequestRecord>
        {
            new(RecentStart.AddDays(1), RequestKind.Leave, 1m),
            new(RecentStart.AddDays(8), RequestKind.Leave, 1.5m),
            new(RecentStart.AddDays(3), RequestKind.ShiftSwap, 0m),
            new(RecentStart.AddDays(10), RequestKind.ShiftSwap, 0m),
        };

        var result = AttritionScorer.Assess(Employee(shifts, requests, Availability(4, 1)), AsOf, new AttritionOptions());

        Assert.Equal(RiskLevel.High, result.Level);
        Assert.InRange(result.Score, 50, 100);
        Assert.Contains(result.Reasons, r => r.Contains("đi muộn"));
        Assert.Contains(result.Reasons, r => r.Contains("Vắng mặt"));
        Assert.Contains(result.Reasons, r => r.Contains("Xin nghỉ"));
        Assert.Contains(result.Reasons, r => r.Contains("Xin đổi ca"));
        Assert.Contains(result.Reasons, r => r.Contains("Đăng ký ca"));

        // Lý do xếp theo mức đóng góp giảm dần
        var contributions = result.Signals.Where(s => s.Reason is not null && s.SubScore >= 0.25)
            .OrderByDescending(s => s.Contribution).Select(s => s.Reason!).ToList();
        Assert.Equal(contributions, result.Reasons);
    }

    [Fact]
    public void Reasons_state_the_numbers_in_vietnamese_format()
    {
        var shifts = History(_ => Week(BaselineStart, late: 1, lateMin: 5), _ => Week(RecentStart, late: 4, lateMin: 20));
        var result = AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions());

        Assert.Contains(result.Reasons, r => r.Contains("Tỷ lệ ca đi muộn tăng từ 17% lên 67%"));
        Assert.Contains(result.Reasons, r => r.Contains("trung bình mỗi ca tăng từ 0,8 lên 13,3 phút"));
    }

    [Fact]
    public void Missing_availability_data_drops_that_signal_and_renormalises_the_rest()
    {
        var shifts = History(
            _ => Week(BaselineStart, late: 1, lateMin: 5),
            _ => Week(RecentStart, late: 4, lateMin: 20, absent: 1, early: 1));
        var result = AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions());

        Assert.DoesNotContain(result.Signals, s => s.Key == "availability");
        Assert.Equal(6, result.Signals.Count);
        Assert.True(result.Score <= 100);
    }

    [Fact]
    public void Employee_who_never_registered_availability_has_no_availability_signal()
    {
        var shifts = History(_ => Week(BaselineStart), _ => Week(RecentStart));
        var result = AttritionScorer.Assess(Employee(shifts, availability: Availability(0, 0)), AsOf, new AttritionOptions());
        Assert.DoesNotContain(result.Signals, s => s.Key == "availability");
    }

    [Fact]
    public void Not_enough_baseline_history_means_no_verdict()
    {
        var shifts = Weeks(RecentStart.AddDays(-14), 1, _ => Week(RecentStart, late: 1, lateMin: 5)); // chỉ 1 tuần nền
        shifts.AddRange(Weeks(RecentStart, 2, _ => Week(RecentStart, late: 6, lateMin: 30)));
        var result = AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions());

        Assert.False(result.DataSufficient);
        Assert.Equal(0, result.Score);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Not_enough_recent_shifts_means_no_verdict()
    {
        var shifts = Weeks(BaselineStart, 8, _ => Week(BaselineStart));
        shifts.Add(new ShiftRecord(RecentStart, true, false, 30, 0)); // chỉ 1 ca gần đây
        Assert.False(AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions()).DataSufficient);
    }

    [Fact]
    public void Improvement_is_not_penalised()
    {
        var shifts = History(_ => Week(BaselineStart, late: 4, lateMin: 15, absent: 1), _ => Week(RecentStart));
        var result = AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions());
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Shifts_on_or_after_the_as_of_date_are_ignored()
    {
        var shifts = History(_ => Week(BaselineStart), _ => Week(RecentStart));
        shifts.AddRange(Enumerable.Range(0, 6).Select(i => new ShiftRecord(AsOf.AddDays(i), false, true, 0, 0)));
        Assert.Equal(0, AttritionScorer.Assess(Employee(shifts), AsOf, new AttritionOptions()).Score);
    }

    [Fact]
    public void Requests_outside_the_windows_are_ignored()
    {
        var shifts = History(_ => Week(BaselineStart), _ => Week(RecentStart));
        var requests = new List<RequestRecord>
        {
            new(BaselineStart.AddDays(-1), RequestKind.Leave, 5m), // trước nền
            new(AsOf, RequestKind.Leave, 5m)                        // từ ngày chấm trở đi
        };
        Assert.Equal(0, AttritionScorer.Assess(Employee(shifts, requests), AsOf, new AttritionOptions()).Score);
    }

    [Fact]
    public void Overtime_and_explanation_requests_do_not_count_as_leave_or_swap()
    {
        var shifts = History(_ => Week(BaselineStart), _ => Week(RecentStart));
        var requests = new List<RequestRecord>
        {
            new(RecentStart.AddDays(1), RequestKind.Overtime, 0m),
            new(RecentStart.AddDays(2), RequestKind.Explanation, 0m)
        };
        Assert.Equal(0, AttritionScorer.Assess(Employee(shifts, requests), AsOf, new AttritionOptions()).Score);
    }

    [Fact]
    public void Thresholds_and_weights_are_configurable()
    {
        var shifts = History(_ => Week(BaselineStart, late: 1, lateMin: 5), _ => Week(RecentStart, late: 3, lateMin: 10));
        var employee = Employee(shifts);

        var normal = AttritionScorer.Assess(employee, AsOf, new AttritionOptions());
        var strict = AttritionScorer.Assess(employee, AsOf, new AttritionOptions { HighThreshold = 5, MediumThreshold = 1 });

        Assert.Equal(normal.Score, strict.Score);
        Assert.Equal(RiskLevel.High, strict.Level);
        Assert.NotEqual(RiskLevel.High, normal.Level);

        var lateOnly = new AttritionOptions
        {
            WeightLateFrequency = 1, WeightLateMinutes = 0, WeightEarlyLeave = 0, WeightAbsence = 0,
            WeightLeaveRequests = 0, WeightShiftSwap = 0, WeightAvailability = 0
        };
        Assert.True(AttritionScorer.Assess(employee, AsOf, lateOnly).Score > normal.Score);
    }

    [Fact]
    public void Score_is_always_between_0_and_100()
    {
        var shifts = History(_ => Week(BaselineStart), _ => Week(RecentStart, late: 3, lateMin: 120, absent: 3));
        var requests = Enumerable.Range(0, 20).Select(i => new RequestRecord(RecentStart.AddDays(i % 14), RequestKind.Leave, 3m)).ToList();
        var result = AttritionScorer.Assess(Employee(shifts, requests, Availability(10, 0)), AsOf, new AttritionOptions());
        Assert.InRange(result.Score, 0, 100);
    }
}
