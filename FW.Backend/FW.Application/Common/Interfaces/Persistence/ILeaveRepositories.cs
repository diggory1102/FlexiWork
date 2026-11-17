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

    /// <summary>
    /// Chốt kết quả duyệt — chỉ khi đơn vẫn đang chờ duyệt (hai người duyệt cùng lúc: người sau nhận false). Gọi trong transaction.
    /// </summary>
    Task<bool> TryCompleteReviewAsync(int id, string status, int reviewerId, DateTime reviewedAt, string? note,
        CancellationToken cancellationToken = default);

    /// <summary>Các đơn nghỉ đã duyệt của những nhân viên này giao với khoảng ngày.</summary>
    Task<IReadOnlyList<ApprovedLeave>> GetApprovedLeavesAsync(IEnumerable<int> userIds, DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken = default);
}

/// <summary>Khoảng ngày nghỉ đã được duyệt của một nhân viên.</summary>
public class ApprovedLeave
{
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;

    public bool Covers(DateTime date) => StartDate.Date <= date.Date && date.Date <= EndDate.Date;
}

/// <summary>Điều kiện tra cứu quỹ phép theo nhân viên (nhân viên chưa nghỉ việc).</summary>
public class LeaveBalanceQuery : PagedQuery
{
    public int Year { get; set; }
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
}

/// <summary>Quỹ phép một năm của một nhân viên; các cột quỹ null = chưa khởi tạo (dùng số ngày mặc định).</summary>
public class LeaveBalanceRow
{
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public decimal? TotalDays { get; set; }
    public decimal? UsedDays { get; set; }
    public decimal? RemainingDays { get; set; }

    /// <summary>Tổng ngày của các đơn trừ phép đang chờ duyệt trong năm.</summary>
    public decimal PendingDays { get; set; }
}

/// <summary>Quỹ phép năm. Các thao tác ghi đều là câu UPDATE có điều kiện (an toàn khi nhiều người thao tác cùng lúc).</summary>
public interface ILeaveBalanceRepository : IGenericRepository<LeaveBalance>
{
    Task<LeaveBalance?> GetByUserAndYearAsync(int userId, int year, CancellationToken cancellationToken = default);

    /// <summary>Tạo quỹ phép năm (tổng = <paramref name="defaultTotalDays"/>) nếu chưa có.</summary>
    Task EnsureExistsAsync(int userId, int year, decimal defaultTotalDays, CancellationToken cancellationToken = default);

    /// <summary>Trừ <paramref name="days"/> ngày vào quỹ — false nếu không đủ ngày còn lại.</summary>
    Task<bool> TryDeductAsync(int userId, int year, decimal days, CancellationToken cancellationToken = default);

    /// <summary>Cộng / trừ tổng quỹ — false nếu tổng mới nhỏ hơn số ngày đã dùng hoặc vượt <paramref name="maxTotalDays"/>.</summary>
    Task<bool> TryAdjustTotalAsync(int userId, int year, decimal deltaDays, decimal maxTotalDays, CancellationToken cancellationToken = default);

    Task<PaginatedResult<LeaveBalanceRow>> GetRowsPagedAsync(LeaveBalanceQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Một lần điều chỉnh quỹ phép kèm tên người điều chỉnh.</summary>
public class LeaveBalanceAdjustmentDetail : LeaveBalanceAdjustment
{
    public string? CreatedByName { get; set; }
}

public interface ILeaveBalanceAdjustmentRepository : IGenericRepository<LeaveBalanceAdjustment>
{
    /// <summary>Lịch sử điều chỉnh của nhân viên trong năm, mới nhất trước.</summary>
    Task<IReadOnlyList<LeaveBalanceAdjustmentDetail>> GetByUserAndYearAsync(int userId, int year, CancellationToken cancellationToken = default);
}
