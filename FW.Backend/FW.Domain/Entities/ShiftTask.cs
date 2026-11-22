using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Một đầu việc mẫu trong checklist của ca (bảng shift_tasks, UC 6.1). Áp dụng cho mọi lịch của ca <see cref="ShiftId"/>
/// tại chi nhánh <see cref="BranchId"/> (null = mọi chi nhánh), cho nhân viên giữ chức vụ <see cref="PositionId"/> (null = mọi người).
/// </summary>
public class ShiftTask : BaseAuditableEntity
{
    public int ShiftId { get; set; }
    public int? BranchId { get; set; }
    public int? PositionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Giá trị thuộc <see cref="TaskPhase"/>.</summary>
    public string Phase { get; set; } = TaskPhase.During;

    /// <summary>Thứ tự trong cùng giai đoạn (nhỏ trước).</summary>
    public int SortOrder { get; set; }

    /// <summary>Bắt buộc hoàn thành trước khi hết ca.</summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>Phải chụp ảnh hiện trường khi tích hoàn thành.</summary>
    public bool RequiresPhoto { get; set; }

    /// <summary>Giá trị thuộc <see cref="TaskAssignMode"/>: mỗi người một bản hoặc làm chung (một người xong là cả ca xong).</summary>
    public string AssignMode { get; set; } = TaskAssignMode.Each;

    public bool IsShared => AssignMode == TaskAssignMode.Shared;
}

/// <summary>Một đầu việc được giao cho nhân viên trong một ca cụ thể (bảng shift_task_assignments).</summary>
public class ShiftTaskAssignment : BaseEntity<long>, IAuditableEntity
{
    public int TaskId { get; set; }
    public long ScheduleId { get; set; }
    public int AssignedTo { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Người đã bấm hoàn thành. Việc làm chung: những người còn lại cũng mang giá trị này (thấy "đã được X làm"). Null ở dữ liệu cũ.</summary>
    public int? CompletedBy { get; set; }

    public string? ProofImageUrl { get; set; }
    public string? ProofImagePublicId { get; set; }
    public string? Note { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
