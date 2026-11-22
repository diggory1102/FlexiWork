using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>Điều kiện lọc mẫu checklist.</summary>
public class ShiftTaskQuery
{
    public int? ShiftId { get; set; }

    /// <summary>Chỉ lấy việc áp dụng cho chi nhánh này (việc chung + việc riêng của chi nhánh); null = mọi việc.</summary>
    public int? BranchId { get; set; }
}

/// <summary>Đầu việc mẫu kèm tên ca, chi nhánh, chức vụ.</summary>
public class ShiftTaskDetail : ShiftTask
{
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? PositionTitle { get; set; }
}

public interface IShiftTaskRepository : IGenericRepository<ShiftTask>
{
    /// <summary>Mẫu checklist (chưa xóa), sắp theo ca → giai đoạn → thứ tự.</summary>
    Task<IReadOnlyList<ShiftTaskDetail>> GetTemplatesAsync(ShiftTaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>Các việc sẽ giao cho một nhân viên khi làm ca này ở chi nhánh này.</summary>
    Task<IReadOnlyList<ShiftTask>> GetApplicableAsync(int shiftId, int branchId, int? positionId, CancellationToken cancellationToken = default);

    /// <summary>Số việc của ca trong cùng phạm vi (cùng chi nhánh, hoặc cùng là việc chung).</summary>
    Task<int> CountInScopeAsync(int shiftId, int? branchId, CancellationToken cancellationToken = default);
}

/// <summary>Việc đã giao kèm nội dung việc và trạng thái ca.</summary>
public class ShiftTaskAssignmentDetail : ShiftTaskAssignment
{
    public int ShiftId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Phase { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsMandatory { get; set; }
    public bool RequiresPhoto { get; set; }

    public string AssignMode { get; set; } = Domain.Enums.TaskAssignMode.Each;

    public int ScheduleUserId { get; set; }
    public int ScheduleBranchId { get; set; }
    public DateTime ScheduleWorkDate { get; set; }
    public string ScheduleStatus { get; set; } = string.Empty;

    /// <summary>Tên người đã hoàn thành (việc làm chung có thể là người khác với người đang xem).</summary>
    public string? CompletedByName { get; set; }

    /// <summary>Người hoàn thành; dữ liệu cũ chưa ghi thì là chính người được giao.</summary>
    public int? EffectiveCompletedBy => IsCompleted ? CompletedBy ?? AssignedTo : null;

    /// <summary>Phần nội dung việc (dùng cho quy tắc sắp xếp / kiểm tra).</summary>
    public ShiftTask Task => new()
    {
        Id = TaskId, ShiftId = ShiftId, Title = Title, Description = Description, Phase = Phase, SortOrder = SortOrder,
        IsMandatory = IsMandatory, RequiresPhoto = RequiresPhoto, AssignMode = AssignMode
    };
}

/// <summary>Tiến độ checklist của một ca đã công bố trong ngày.</summary>
public class ShiftTaskProgressRow
{
    public long ScheduleId { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string ScheduleStatus { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Done { get; set; }
    public int MandatoryTotal { get; set; }
    public int MandatoryDone { get; set; }

    /// <summary>Số việc mẫu sẽ giao cho người làm ca này (theo ca, chi nhánh, chức vụ) — để hiện "dự kiến" khi họ chưa check-in.</summary>
    public int Expected { get; set; }

    public int ExpectedMandatory { get; set; }
}

/// <summary>Một việc làm chung trong ngày của một chi nhánh / ca: ai đã làm, hay chưa ai nhận.</summary>
public class SharedTaskProgressRow
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>Số người đang được giao việc này trong ca.</summary>
    public int Participants { get; set; }

    public bool IsDone { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public interface IShiftTaskAssignmentRepository : IGenericRepository<ShiftTaskAssignment, long>
{
    /// <summary>
    /// Giao các việc áp dụng cho ca (chưa giao) cho nhân viên làm ca — chạy lại được (UNIQUE(schedule_id, task_id)),
    /// nên cũng dùng để bổ sung việc mới thêm vào mẫu khi ca đang diễn ra.
    /// </summary>
    Task AssignForScheduleAsync(long scheduleId, int userId, int shiftId, int branchId, int? positionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Giao một việc mẫu vừa thêm / sửa cho MỌI người đang trong ca (đã check-in, chưa check-out) mà việc áp dụng cho — để việc mới không chỉ
    /// rơi vào người nào đó vừa mở checklist. Người chưa vào ca sẽ nhận khi check-in. Chạy lại được.
    /// </summary>
    /// <returns>Số người vừa được giao thêm việc.</returns>
    Task<int> AssignTemplateToInProgressAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bổ sung các việc còn thiếu cho mọi ca ĐANG diễn ra trong ngày (và chi nhánh): sửa dữ liệu của những việc mẫu thêm trước khi có
    /// <see cref="AssignTemplateToInProgressAsync"/> và bất kỳ trường hợp lệch nào. Chạy lại được.
    /// </summary>
    Task<int> AssignForInProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftTaskAssignmentDetail>> GetDetailsByScheduleIdsAsync(IEnumerable<long> scheduleIds,
        CancellationToken cancellationToken = default);

    Task<ShiftTaskAssignmentDetail?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Số việc được giao trong ca / số việc chưa xong (tùy chọn chỉ tính việc bắt buộc).</summary>
    Task<(int Total, int Pending)> CountAsync(long scheduleId, bool mandatoryOnly, CancellationToken cancellationToken = default);

    /// <summary>Tiến độ các ca đã công bố (chưa hủy, không nghỉ) trong ngày; branchId null = mọi chi nhánh.</summary>
    Task<IReadOnlyList<ShiftTaskProgressRow>> GetProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Các việc làm chung trong ngày (đã có người được giao), theo chi nhánh / ca — để Quản lý thấy ai đã làm và việc nào chưa ai nhận.</summary>
    Task<IReadOnlyList<SharedTaskProgressRow>> GetSharedProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi hoàn thành một việc, an toàn khi nhiều người bấm cùng lúc: khóa các bản việc cùng nhóm rồi ghi trong một transaction.
    /// Việc làm chung: cả nhóm (cùng việc mẫu, chi nhánh, ngày) được đánh dấu xong bởi người này. Chỉ người đến trước thắng.
    /// </summary>
    /// <returns>false nếu việc đã được hoàn thành trước đó (người khác vừa bấm, hoặc chính bản này đã xong).</returns>
    Task<bool> TryCompleteAsync(ShiftTaskAssignmentDetail assignment, DateTime completedAt, string? proofImageUrl, string? proofImagePublicId,
        string? note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bỏ tích. Việc làm chung: bỏ cho cả nhóm đã được đánh dấu xong bởi cùng người hoàn thành.
    /// </summary>
    /// <returns>Mã các ảnh minh chứng đã bị gỡ (để xóa trên dịch vụ lưu ảnh).</returns>
    Task<IReadOnlyList<string>> UncompleteAsync(ShiftTaskAssignmentDetail assignment, CancellationToken cancellationToken = default);
}
