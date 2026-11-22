using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Services;

namespace FW.Application.Features.ShiftTasks;

// ---------------------------------------------------------------- Mẫu checklist

/// <param name="CanEdit">Người đang xem sửa / xóa được việc này (Quản lý chỉ sửa việc riêng của chi nhánh mình).</param>
public record ShiftTaskTemplateDto(
    int Id, int ShiftId, string ShiftCode, string ShiftName, int? BranchId, string? BranchName, int? PositionId, string? PositionTitle,
    string Title, string? Description, string Phase, int SortOrder, bool IsMandatory, bool RequiresPhoto, bool CanEdit)
{
    public static ShiftTaskTemplateDto From(ShiftTaskDetail t, bool canEdit) => new(
        t.Id, t.ShiftId, t.ShiftCode, t.ShiftName, t.BranchId, t.BranchName, t.PositionId, t.PositionTitle,
        t.Title, t.Description, t.Phase, t.SortOrder, t.IsMandatory, t.RequiresPhoto, canEdit);
}

/// <summary>
/// Thêm / sửa đầu việc mẫu. Ca và chi nhánh chỉ đặt khi tạo (sửa thì giữ nguyên — muốn chuyển ca hãy tạo việc mới).
/// </summary>
public class ShiftTaskTemplateRequest
{
    [Required(ErrorMessage = "Vui lòng chọn ca.")]
    public int? ShiftId { get; set; }

    /// <summary>Null = mọi chi nhánh (chỉ Admin, với ca dùng chung). Quản lý: luôn là chi nhánh mình.</summary>
    public int? BranchId { get; set; }

    /// <summary>Null = mọi nhân viên của ca.</summary>
    public int? PositionId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên việc.")]
    [MaxLength(ShiftTaskRules.MaxTitleLength, ErrorMessage = "Tên việc tối đa 150 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }

    /// <summary>start, during, end.</summary>
    public string Phase { get; set; } = Domain.Enums.TaskPhase.During;

    [Range(0, 999, ErrorMessage = "Thứ tự từ 0 đến 999.")]
    public int SortOrder { get; set; }

    public bool IsMandatory { get; set; } = true;
    public bool RequiresPhoto { get; set; }
}

public class ShiftTaskTemplateFilter
{
    public int? ShiftId { get; set; }

    /// <summary>Admin/HR: lọc việc áp dụng cho chi nhánh; Quản lý: luôn là chi nhánh mình.</summary>
    public int? BranchId { get; set; }
}

// ---------------------------------------------------------------- Checklist trong ca

/// <param name="AssignmentId">Null = việc dự kiến (ca chưa check-in, chưa giao).</param>
public record TaskItemDto(
    long? AssignmentId, int TaskId, string Title, string? Description, string Phase, int SortOrder, bool IsMandatory, bool RequiresPhoto,
    bool IsCompleted, DateTime? CompletedAt, string? ProofImageUrl, string? Note)
{
    public static TaskItemDto From(ShiftTaskAssignmentDetail a) => new(
        a.Id, a.TaskId, a.Title, a.Description, a.Phase, a.SortOrder, a.IsMandatory, a.RequiresPhoto,
        a.IsCompleted, a.CompletedAt, a.ProofImageUrl, a.Note);

    public static TaskItemDto Preview(Domain.Entities.ShiftTask t) => new(
        null, t.Id, t.Title, t.Description, t.Phase, t.SortOrder, t.IsMandatory, t.RequiresPhoto, false, null, null, null);
}

/// <summary>Checklist của một ca.</summary>
/// <param name="IsAssigned">Đã giao việc (đã check-in); false = danh sách dự kiến.</param>
/// <param name="CanUpdate">Người xem là người làm ca và ca đang diễn ra — tích / bỏ tích được.</param>
public record ScheduleTasksDto(
    long ScheduleId, int UserId, string EmployeeCode, string FullName, DateTime WorkDate, string ShiftName, string StartTime, string EndTime,
    string ScheduleStatus, bool IsAssigned, bool CanUpdate, int Total, int Done, int? Percent, IReadOnlyList<TaskItemDto> Tasks);

/// <summary>Tích hoàn thành (form-data, "file" = ảnh hiện trường).</summary>
public class CompleteTaskForm
{
    [MaxLength(ShiftTaskRules.MaxNoteLength, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

// ---------------------------------------------------------------- Tiến độ

public record ScheduleProgressDto(
    long ScheduleId, int UserId, string EmployeeCode, string FullName, string? AvatarUrl, int BranchId, string BranchName,
    string ShiftName, string StartTime, string EndTime, string ScheduleStatus, int Total, int Done, int MandatoryTotal, int MandatoryDone,
    int? Percent, int Expected, int ExpectedMandatory)
{
    public static ScheduleProgressDto From(ShiftTaskProgressRow r) => new(
        r.ScheduleId, r.UserId, r.EmployeeCode, r.FullName, r.AvatarUrl, r.BranchId, r.BranchName, r.ShiftName,
        r.StartTime.ToString(@"hh\:mm"), r.EndTime.ToString(@"hh\:mm"), r.ScheduleStatus, r.Total, r.Done, r.MandatoryTotal, r.MandatoryDone,
        ShiftTaskRules.Percent(r.Done, r.Total), r.Expected, r.ExpectedMandatory);
}

/// <summary>Tiến độ checklist toàn chi nhánh (hoặc toàn doanh nghiệp) trong ngày (UC 6.4).</summary>
/// <param name="MandatoryPending">Số việc bắt buộc chưa xong (của các ca đã giao việc).</param>
/// <param name="NotStarted">Số ca chưa check-in (chưa giao việc).</param>
public record TaskProgressDto(
    DateTime Date, int? BranchId, int TotalTasks, int DoneTasks, int? Percent, int MandatoryPending, int NotStarted,
    IReadOnlyList<ScheduleProgressDto> Schedules);

public class TaskProgressFilter
{
    /// <summary>Mặc định hôm nay.</summary>
    public DateOnly? Date { get; set; }

    public int? BranchId { get; set; }
}
