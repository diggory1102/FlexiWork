using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>
/// Quy tắc checklist công việc theo ca (Task 4.3):
/// <list type="bullet">
/// <item>Một đầu việc mẫu áp dụng cho lịch khi cùng ca, chi nhánh khớp (hoặc mẫu dùng mọi chi nhánh) và chức vụ khớp (hoặc mẫu cho mọi người).</item>
/// <item>Việc được giao lúc nhân viên check-in; chỉ tích / bỏ tích khi đang trong ca (đã check-in, chưa check-out).</item>
/// <item>Việc bắt buộc ảnh phải kèm ảnh hiện trường khi tích hoàn thành.</item>
/// <item>Tiến độ = số việc xong / số việc được giao (làm tròn %); chưa giao việc nào thì chưa tính tiến độ.</item>
/// </list>
/// </summary>
public static class ShiftTaskRules
{
    public const int MaxTasksPerShift = 30;
    public const int MaxTitleLength = 150;
    public const int MaxNoteLength = 255;

    public static bool AppliesTo(ShiftTask task, int shiftId, int branchId, int? positionId) =>
        !task.IsDeleted &&
        task.ShiftId == shiftId &&
        (task.BranchId is null || task.BranchId == branchId) &&
        (task.PositionId is null || task.PositionId == positionId);

    /// <summary>Sắp checklist: đầu ca → trong ca → cuối ca, rồi theo thứ tự đặt, rồi theo thứ tự tạo.</summary>
    public static IEnumerable<T> Sort<T>(IEnumerable<T> items, Func<T, ShiftTask> task) =>
        items.OrderBy(i => TaskPhase.Order(task(i).Phase)).ThenBy(i => task(i).SortOrder).ThenBy(i => task(i).Id);

    /// <returns>Lý do không tích / bỏ tích được; null nếu được.</returns>
    public static string? ValidateCanUpdate(Schedule schedule) => schedule.Status switch
    {
        ScheduleStatus.InProgress => null,
        ScheduleStatus.Completed => "Ca đã check-out — không thay đổi checklist được nữa.",
        _ => "Chỉ tích việc khi đang trong ca (đã check-in)."
    };

    /// <summary>Tích hoàn thành: việc bắt buộc ảnh thì phải có ảnh.</summary>
    public static string? ValidateCompletion(ShiftTask task, bool hasPhoto) =>
        task.RequiresPhoto && !hasPhoto ? $"Việc \"{task.Title}\" cần chụp ảnh hiện trường khi hoàn thành." : null;

    /// <summary>Phần trăm hoàn thành (làm tròn); chưa có việc → null.</summary>
    public static int? Percent(int done, int total) =>
        total <= 0 ? null : (int)Math.Round(done * 100m / total, MidpointRounding.AwayFromZero);
}
