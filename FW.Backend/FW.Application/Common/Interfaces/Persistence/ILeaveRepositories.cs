using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>Loại đơn từ (xóa mềm).</summary>
public interface ILeaveTypeRepository : IGenericRepository<LeaveType>
{
    /// <summary>Loại đơn chưa xóa, sắp theo nhóm → thứ tự khai báo; <paramref name="activeOnly"/> = chỉ loại đang nhận đơn.</summary>
    Task<IReadOnlyList<LeaveType>> GetListAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Số đơn (mọi trạng thái) / số đơn đang xử lý thuộc loại này.</summary>
    Task<(int Total, int Open)> CountRequestsAsync(int leaveTypeId, CancellationToken cancellationToken = default);
}

/// <summary>Điều kiện tra cứu đơn. Khoảng ngày: đơn có [start_date, end_date] giao với [FromDate, ToDate].</summary>
public class LeaveRequestQuery : PagedQuery
{
    public int? UserId { get; set; }
    public int? TargetUserId { get; set; }
    public int? BranchId { get; set; }
    public IReadOnlyList<string>? Statuses { get; set; }
    public string? Category { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>Đơn kèm thông tin hiển thị (người tạo, loại đơn, ca đổi, lượt chấm công được giải trình, người duyệt).</summary>
public class LeaveRequestDetail : LeaveRequest
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? BranchName { get; set; }

    public string LeaveTypeName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public bool DeductsBalance { get; set; }

    public string? TargetEmployeeCode { get; set; }
    public string? TargetFullName { get; set; }
    public string? TargetAvatarUrl { get; set; }

    public DateTime? SourceWorkDate { get; set; }
    public string? SourceShiftName { get; set; }
    public TimeSpan? SourceStartTime { get; set; }
    public TimeSpan? SourceEndTime { get; set; }
    public string? SourceScheduleStatus { get; set; }

    public DateTime? TargetWorkDate { get; set; }
    public string? TargetShiftName { get; set; }
    public TimeSpan? TargetStartTime { get; set; }
    public TimeSpan? TargetEndTime { get; set; }
    public string? TargetScheduleStatus { get; set; }

    public DateTime? LogWorkDate { get; set; }
    public DateTime? LogCheckinTime { get; set; }
    public DateTime? LogCheckoutTime { get; set; }
    public int? LogLateMinutes { get; set; }
    public int? LogEarlyMinutes { get; set; }
    public string? LogStatus { get; set; }
    public string? LogShiftName { get; set; }

    public string? ApproverName { get; set; }
}

public interface ILeaveRequestRepository : IGenericRepository<LeaveRequest>
{
    /// <summary>Tra cứu có phân trang, mới nhất trước.</summary>
    Task<PaginatedResult<LeaveRequestDetail>> GetDetailsPagedAsync(LeaveRequestQuery query, CancellationToken cancellationToken = default);

    Task<LeaveRequestDetail?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên đã có đơn nghỉ (chờ / đã duyệt) giao với khoảng ngày này chưa.</summary>
    Task<bool> HasOverlappingLeaveAsync(int userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên đã có đơn OT (chờ / đã duyệt) giao với khoảng giờ này chưa.</summary>
    Task<bool> HasOverlappingOvertimeAsync(int userId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default);

    /// <summary>Có đơn đổi ca đang xử lý nào dính tới một trong các ca này không (bỏ qua đơn <paramref name="excludeId"/>).</summary>
    Task<bool> HasOpenSwapForSchedulesAsync(IReadOnlyCollection<long> scheduleIds, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Lượt chấm công đã có đơn giải trình (chờ / đã duyệt) chưa.</summary>
    Task<bool> HasActiveExplanationAsync(long attendanceLogId, CancellationToken cancellationToken = default);

    /// <summary>Tổng số ngày của các đơn nghỉ trừ phép năm đang chờ duyệt, bắt đầu trong năm <paramref name="year"/>.</summary>
    Task<decimal> GetPendingDeductibleDaysAsync(int userId, int year, CancellationToken cancellationToken = default);
}

/// <summary>Quỹ phép năm.</summary>
public interface ILeaveBalanceRepository : IGenericRepository<LeaveBalance>
{
    Task<LeaveBalance?> GetByUserAndYearAsync(int userId, int year, CancellationToken cancellationToken = default);
}
