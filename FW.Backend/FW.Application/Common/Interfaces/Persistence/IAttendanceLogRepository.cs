using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>Điều kiện tra cứu nhật ký chấm công (khoảng ngày công tính cả hai đầu).</summary>
public class AttendanceLogQuery : PagedQuery
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>null = mọi chi nhánh (Admin/HR).</summary>
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
    public string? Status { get; set; }
}

/// <summary>Bản ghi chấm công kèm thông tin nhân viên, chi nhánh, ca và người duyệt (đọc bằng một truy vấn JOIN).</summary>
public class AttendanceLogDetail : AttendanceLog
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal BranchLatitude { get; set; }
    public decimal BranchLongitude { get; set; }
    public int BranchRadius { get; set; }

    /// <summary>Chi nhánh đang khai báo Wi-Fi (lớp 2 có áp dụng) — phân biệt "sai Wi-Fi" với "không yêu cầu Wi-Fi".</summary>
    public bool BranchRequiresWifi { get; set; }

    /// <summary>Thông tin ca; null nếu lịch đã bị xóa.</summary>
    public string? ShiftCode { get; set; }
    public string? ShiftName { get; set; }
    public TimeSpan? ShiftStartTime { get; set; }
    public TimeSpan? ShiftEndTime { get; set; }

    public string? ReviewerName { get; set; }
    public string? CheckoutAdjusterName { get; set; }
}

public interface IAttendanceLogRepository : IGenericRepository<AttendanceLog, long>
{
    /// <summary>Bản ghi chấm công của một ca (mỗi ca tối đa một bản ghi).</summary>
    Task<AttendanceLog?> GetByScheduleIdAsync(long scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu có phân trang, mới nhất trước (ngày công giảm dần, giờ vào giảm dần).</summary>
    Task<PaginatedResult<AttendanceLogDetail>> GetDetailsPagedAsync(AttendanceLogQuery query, CancellationToken cancellationToken = default);

    Task<AttendanceLogDetail?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Bản ghi chấm công của nhiều ca (bỏ qua ca chưa chấm công).</summary>
    Task<IReadOnlyList<AttendanceLog>> GetByScheduleIdsAsync(IEnumerable<long> scheduleIds, CancellationToken cancellationToken = default);

    /// <summary>Lượt đã check-in nhưng chưa check-out gần nhất của nhân viên (đang trong ca).</summary>
    Task<AttendanceLog?> GetOpenByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhân viên KHÁC đã check-in bằng cùng thiết bị trong ngày công — dấu hiệu một điện thoại chấm công hộ nhiều người.
    /// </summary>
    Task<IReadOnlyList<int>> GetOtherUsersOnDeviceAsync(string deviceId, DateTime workDate, int excludeUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>Nhật ký mọi lượt bấm chấm công, kể cả bị từ chối (Task 6.5).</summary>
public interface IAttendanceAttemptRepository : IGenericRepository<AttendanceAttempt, long>
{
    /// <summary>Các lượt có <c>from ≤ attempted_at &lt; to</c>, cũ trước.</summary>
    Task<IReadOnlyList<AttendanceAttempt>> GetByPeriodAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
