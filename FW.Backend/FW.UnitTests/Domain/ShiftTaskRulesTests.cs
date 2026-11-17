using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class ShiftTaskRulesTests
{
    private static ShiftTask Task(int id = 1, int shiftId = 1, int? branchId = null, int? positionId = null, string phase = TaskPhase.During,
        int sort = 0, bool photo = false) =>
        new() { Id = id, ShiftId = shiftId, BranchId = branchId, PositionId = positionId, Phase = phase, SortOrder = sort, RequiresPhoto = photo, Title = $"Việc {id}" };

    [Theory]
    [InlineData(null, null, 1, 7, true)]   // mẫu chung: mọi chi nhánh, mọi người
    [InlineData(1, null, 1, null, true)]   // việc riêng chi nhánh 1
    [InlineData(2, null, 1, 7, false)]     // việc của chi nhánh khác
    [InlineData(null, 7, 1, 7, true)]      // đúng chức vụ
    [InlineData(null, 7, 1, 8, false)]     // khác chức vụ
    [InlineData(null, 7, 1, null, false)]  // nhân viên chưa có chức vụ
    public void Task_applies_by_branch_and_position(int? taskBranch, int? taskPosition, int branchId, int? positionId, bool expected) =>
        Assert.Equal(expected, ShiftTaskRules.AppliesTo(Task(branchId: taskBranch, positionId: taskPosition), 1, branchId, positionId));

    [Fact]
    public void Task_of_another_shift_or_deleted_does_not_apply()
    {
        Assert.False(ShiftTaskRules.AppliesTo(Task(shiftId: 2), 1, 1, null));
        var deleted = Task();
        deleted.IsDeleted = true;
        Assert.False(ShiftTaskRules.AppliesTo(deleted, 1, 1, null));
    }

    [Fact]
    public void Checklist_is_sorted_by_phase_then_order_then_id()
    {
        var tasks = new[]
        {
            Task(1, phase: TaskPhase.End), Task(2, phase: TaskPhase.Start, sort: 2), Task(3, phase: TaskPhase.During),
            Task(4, phase: TaskPhase.Start, sort: 1), Task(5, phase: TaskPhase.Start, sort: 1)
        };
        Assert.Equal(new[] { 4, 5, 2, 3, 1 }, ShiftTaskRules.Sort(tasks, t => t).Select(t => t.Id));
    }

    [Theory]
    [InlineData(ScheduleStatus.InProgress, null)]
    [InlineData(ScheduleStatus.Completed, "đã check-out")]
    [InlineData(ScheduleStatus.Scheduled, "đã check-in")]
    [InlineData(ScheduleStatus.OnLeave, "đã check-in")]
    public void Checklist_changes_only_during_the_shift(string status, string? error)
    {
        var result = ShiftTaskRules.ValidateCanUpdate(new Schedule { Status = status });
        if (error is null) Assert.Null(result);
        else Assert.Contains(error, result);
    }

    [Fact]
    public void Photo_tasks_need_a_photo()
    {
        Assert.Contains("chụp ảnh", ShiftTaskRules.ValidateCompletion(Task(photo: true), hasPhoto: false));
        Assert.Null(ShiftTaskRules.ValidateCompletion(Task(photo: true), hasPhoto: true));
        Assert.Null(ShiftTaskRules.ValidateCompletion(Task(), hasPhoto: false));
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)] // 12,5 → 13
    [InlineData(3, 3, 100)]
    public void Percent_rounds_half_up(int done, int total, int? expected) => Assert.Equal(expected, ShiftTaskRules.Percent(done, total));
}
