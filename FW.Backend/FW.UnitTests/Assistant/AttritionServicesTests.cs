using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.UnitTests.Assistant;

public class AttritionServicesTests
{
    private static async Task<World> ScoredWorld()
    {
        var w = new World();
        await w.NewScoring().RunAsync();
        return w;
    }

    private static AttritionCaller Caller(TestUser u) => new(u.UserId!.Value, u.BranchId, u.Roles);

    [Fact]
    public async Task Weekly_run_scores_everyone_and_flags_the_deteriorating_employees()
    {
        var w = new World();
        var summary = await w.NewScoring().RunAsync();

        Assert.Equal(new DateOnly(2026, 11, 16), summary.WeekStart);
        Assert.Equal(7, summary.Employees);   // 3,5,6,7,8,9,10
        Assert.Equal(6, summary.Scored);      // nhân viên 9 mới vào → chưa đủ dữ liệu
        Assert.Equal(1, summary.High);
        Assert.Equal(1, summary.Medium);

        var latest = (await w.Attrition.GetLatestAsync(null)).ToDictionary(s => s.UserId);
        Assert.Equal(RiskLevel.High, latest[5].Level);
        Assert.True(latest[5].Score >= 70, $"Lan score={latest[5].Score}");
        Assert.Equal(RiskLevel.Medium, latest[6].Level);
        Assert.False(latest[9].DataSufficient);
        foreach (var id in new[] { 3, 7, 8, 10 })
            Assert.Equal(RiskLevel.Low, latest[id].Level);
    }

    [Fact]
    public async Task Rerunning_the_same_week_does_not_duplicate_or_lose_the_previous_week_score()
    {
        var w = new World();
        var scoring = w.NewScoring();
        await scoring.RunAsync(new DateOnly(2026, 11, 9));  // tuần trước
        await scoring.RunAsync();                            // tuần này
        await scoring.RunAsync();                            // chạy lại tuần này

        var history = await w.Attrition.GetHistoryAsync(5, 10);
        Assert.Equal(2, history.Count);
        Assert.Equal(new DateOnly(2026, 11, 9), history[0].WeekStart);
        Assert.Null(history[0].PreviousScore);
        Assert.Equal(history[0].Score, history[1].PreviousScore);
        Assert.Equal(history[1].Score - history[0].Score, history[1].Delta);
    }

    [Fact]
    public async Task Run_uses_the_monday_of_the_given_date()
    {
        var w = new World();
        var summary = await w.NewScoring().RunAsync(new DateOnly(2026, 11, 20)); // Thứ Sáu
        Assert.Equal(new DateOnly(2026, 11, 16), summary.WeekStart);
    }

    [Fact]
    public async Task Manager_sees_only_their_branch_ordered_by_score()
    {
        var w = await ScoredWorld();
        var alerts = await w.NewQuery().GetAlertsAsync(Caller(TestUser.Manager(2, 1)), null, RiskLevel.Medium, 50);

        Assert.Equal(new[] { 5, 6 }, alerts.Select(a => a.UserId));
        Assert.All(alerts, a => Assert.Equal(1, a.BranchId));
        Assert.NotEmpty(alerts[0].Reasons);
        Assert.True(alerts[0].Score > alerts[1].Score);
    }

    [Fact]
    public async Task High_level_filter_excludes_medium_and_insufficient_data()
    {
        var w = await ScoredWorld();
        var alerts = await w.NewQuery().GetAlertsAsync(Caller(TestUser.Hr()), null, RiskLevel.High, 50);
        Assert.Equal(new[] { 5 }, alerts.Select(a => a.UserId));
    }

    [Fact]
    public async Task Hr_sees_all_branches_and_can_filter_by_branch()
    {
        var w = await ScoredWorld();
        // Đẩy Bùi Văn Nam (chi nhánh 2) vào trạng thái xấu để có dữ liệu chi nhánh 2
        var nam = w.Attrition.Activities.Single(a => a.UserId == 10);
        w.Attrition.Activities.Remove(nam);
        w.Attrition.Activities.Add(w.Attrition.Activities.Single(a => a.UserId == 5) with { UserId = 10, Name = "Bùi Văn Nam", BranchId = 2 });
        await w.NewScoring().RunAsync();

        var all = await w.NewQuery().GetAlertsAsync(Caller(TestUser.Hr()), null, RiskLevel.Medium, 50);
        Assert.Contains(all, a => a.BranchId == 2);
        Assert.Contains(all, a => a.BranchId == 1);

        var only2 = await w.NewQuery().GetAlertsAsync(Caller(TestUser.Hr()), 2, RiskLevel.Medium, 50);
        Assert.All(only2, a => Assert.Equal(2, a.BranchId));
    }

    [Fact]
    public async Task Manager_cannot_look_at_another_branch_even_by_asking()
    {
        var w = await ScoredWorld();
        var query = w.NewQuery();

        var other = TestUser.Manager(11, 2);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => query.GetAlertsAsync(Caller(other), 1, RiskLevel.Medium, 50));
        
        // Không chỉ định chi nhánh → tự bị giới hạn vào chi nhánh của mình, không lộ nhân viên chi nhánh 1
        var own = await query.GetAlertsAsync(Caller(other), null, RiskLevel.Medium, 50);
        Assert.DoesNotContain(own, a => a.BranchId == 1);
    }

    [Fact]
    public async Task Employee_and_unbranched_manager_are_refused()
    {
        var w = await ScoredWorld();
        var query = w.NewQuery();

        var e1 = await Assert.ThrowsAsync<ForbiddenException>(() => query.GetAlertsAsync(Caller(TestUser.Employee()), null, RiskLevel.Medium, 10));
        
        var noBranch = new TestUser { UserId = 20, BranchId = null, Roles = new[] { RoleNames.Manager } };
        var e2 = await Assert.ThrowsAsync<ForbiddenException>(() => query.GetAlertsAsync(Caller(noBranch), null, RiskLevel.Medium, 10));
        }

    [Fact]
    public async Task Trend_is_limited_to_the_managers_branch_and_hides_existence_of_others()
    {
        var w = new World();
        await w.NewScoring().RunAsync(new DateOnly(2026, 11, 9));
        await w.NewScoring().RunAsync();
        var query = w.NewQuery();

        var mine = await query.GetTrendAsync(Caller(TestUser.Manager(2, 1)), 5, 12);
        Assert.Equal(2, mine.Count);
        Assert.True(mine[0].WeekStart < mine[1].WeekStart);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => query.GetTrendAsync(Caller(TestUser.Manager(11, 2)), 5, 12));
        
        Assert.Equal(2, (await query.GetTrendAsync(Caller(TestUser.Hr()), 5, 12)).Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => query.GetTrendAsync(Caller(TestUser.Employee(5)), 5, 12));
    }

    // ----------------------------------------------------------------------------- Narrator

    [Fact]
    public async Task Narrator_uses_the_llm_with_only_the_employees_facts_and_safe_instructions()
    {
        var w = await ScoredWorld();
        var snapshot = (await w.Attrition.GetLatestAsync(null)).Single(s => s.UserId == 5);
        w.Llm.Say("Lan có vài biến động về giờ giấc, nên trao đổi thân thiện.");

        var text = await new AttritionNarrator(w.Llm, NullLogger<AttritionNarrator>.Instance).SummarizeAsync(snapshot);

        Assert.Contains("trao đổi thân thiện", text);
        var call = w.Llm.Calls.Single();
        Assert.Empty(call.Tools);
        Assert.Contains("không nhắc kỷ luật", call.Messages[0].Content);
        Assert.Contains("Nguyễn Thị Lan", call.Messages[1].Content);
        Assert.DoesNotContain("Phạm Minh Tuấn", call.Messages[1].Content);
    }

    [Fact]
    public async Task Narrator_falls_back_to_a_built_in_summary_when_the_llm_is_down()
    {
        var w = await ScoredWorld();
        var snapshot = (await w.Attrition.GetLatestAsync(null)).Single(s => s.UserId == 5);
        w.Llm.ThrowOnCall = new LlmUnavailableException("down");

        var text = await new AttritionNarrator(w.Llm, NullLogger<AttritionNarrator>.Instance).SummarizeAsync(snapshot);

        Assert.Contains("Nguyễn Thị Lan", text);
        Assert.Contains("tham khảo", text);
        Assert.Contains(snapshot.Reasons[0], text);
    }

    [Fact]
    public async Task Narrator_does_not_call_the_llm_when_data_is_insufficient()
    {
        var w = await ScoredWorld();
        var snapshot = (await w.Attrition.GetLatestAsync(null)).Single(s => s.UserId == 9);
        var text = await new AttritionNarrator(w.Llm, NullLogger<AttritionNarrator>.Instance).SummarizeAsync(snapshot);

        Assert.Empty(w.Llm.Calls);
        Assert.Contains("Chưa đủ dữ liệu", text);
    }
}
