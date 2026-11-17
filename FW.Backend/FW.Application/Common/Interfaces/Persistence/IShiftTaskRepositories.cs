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

    public int ScheduleUserId { get; set; }
    public int ScheduleBranchId { get; set; }
    public string ScheduleStatus { get; set; } = string.Empty;

    /// <summary>Phần nội dung việc (dùng cho quy tắc sắp xếp / kiểm tra).</summary>
    public ShiftTask Task => new()
    {
        Id = TaskId, ShiftId = ShiftId, Title = Title, Description = Description, Phase = Phase, SortOrder = SortOrder,
        IsMandatory = IsMandatory, RequiresPhoto = RequiresPhoto
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
}

public interface IShiftTaskAssignmentRepository : IGenericRepository<ShiftTaskAssignment, long>
{
    /// <summary>
    /// Giao các việc áp dụng cho ca (chưa giao) cho nhân viên làm ca — chạy lại được (UNIQUE(schedule_id, task_id)),
    /// nên cũng dùng để bổ sung việc mới thêm vào mẫu khi ca đang diễn ra.
    /// </summary>
    Task AssignForScheduleAsync(long scheduleId, int userId, int shiftId, int branchId, int? positionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftTaskAssignmentDetail>> GetDetailsByScheduleIdsAsync(IEnumerable<long> scheduleIds,
        CancellationToken cancellationToken = default);

    Task<ShiftTaskAssignmentDetail?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Số việc được giao trong ca / số việc chưa xong (tùy chọn chỉ tính việc bắt buộc).</summary>
    Task<(int Total, int Pending)> CountAsync(long scheduleId, bool mandatoryOnly, CancellationToken cancellationToken = default);

    /// <summary>Tiến độ các ca đã công bố (chưa hủy, không nghỉ) trong ngày; branchId null = mọi chi nhánh.</summary>
    Task<IReadOnlyList<ShiftTaskProgressRow>> GetProgressAsync(DateTime workDate, int? branchId, CancellationToken cancellationToken = default);
}
