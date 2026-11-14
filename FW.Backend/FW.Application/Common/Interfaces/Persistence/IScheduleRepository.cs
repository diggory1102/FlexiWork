using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Điều kiện tra cứu lịch theo khoảng ngày (bao gồm cả hai đầu).
/// </summary>
public class ScheduleQuery
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? BranchId { get; set; }
    public int? UserId { get; set; }

    /// <summary>Chỉ lấy lịch đã công bố (góc nhìn của nhân viên).</summary>
    public bool PublishedOnly { get; set; }
}

/// <summary>
/// Một dòng lịch kèm thông tin nhân viên và khung ca (đọc bằng một truy vấn JOIN để hiển thị lưới lịch).
/// </summary>
public class ScheduleDetail
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int ShiftId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsCrossDay { get; set; }
    public int BranchId { get; set; }
    public DateTime WorkDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
}

/// <summary>
/// Lịch phân ca. Không xóa mềm: <see cref="IGenericRepository{T,TKey}.DeleteAsync"/> xóa hẳn (chỉ dùng cho lịch nháp).
/// </summary>
public interface IScheduleRepository : IGenericRepository<Schedule, long>
{
    /// <summary>Lịch trong khoảng ngày kèm tên nhân viên và khung ca, sắp theo ngày → giờ bắt đầu → họ tên.</summary>
    Task<IReadOnlyList<ScheduleDetail>> GetDetailsAsync(ScheduleQuery query, CancellationToken cancellationToken = default);

    /// <summary>Lịch (chưa hủy) của một nhân viên trong khoảng ngày — dùng kiểm tra chồng giờ khi xếp ca.</summary>
    Task<IReadOnlyList<Schedule>> GetActiveByUserAsync(int userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>Lịch (chưa hủy) của nhiều nhân viên trong khoảng ngày — kiểm tra chồng giờ cho cả lô trong một truy vấn.</summary>
    Task<IReadOnlyList<Schedule>> GetActiveByUsersAsync(IEnumerable<int> userIds, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>Xóa hẳn các lịch NHÁP của chi nhánh trong khoảng ngày.</summary>
    /// <returns>Số lịch đã xóa.</returns>
    Task<int> DeleteDraftsAsync(int branchId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>Đã có lịch cùng nhân viên, cùng ca, cùng ngày chưa (ràng buộc UNIQUE uq_user_shift_date).</summary>
    Task<bool> ExistsAsync(int userId, int shiftId, DateTime workDate, CancellationToken cancellationToken = default);

    /// <summary>Thêm nhiều ca một lần (xếp lịch cả tuần) bằng câu INSERT nhiều dòng; nên gọi trong transaction.</summary>
    /// <returns>Số dòng đã thêm.</returns>
    Task<int> AddRangeAsync(IReadOnlyCollection<Schedule> schedules, CancellationToken cancellationToken = default);

    /// <summary>Công bố toàn bộ lịch nháp của chi nhánh trong khoảng ngày.</summary>
    /// <returns>Số ca vừa được công bố.</returns>
    Task<int> PublishAsync(int branchId, DateTime fromDate, DateTime toDate, DateTime publishedAt, CancellationToken cancellationToken = default);
}
