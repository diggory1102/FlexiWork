using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public class TimesheetQuery : PagedQuery
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Chi nhánh của bảng công; null = mọi chi nhánh.</summary>
    public int? BranchId { get; set; }

    public int? UserId { get; set; }
    public string? Status { get; set; }
}

/// <summary>Bảng công kèm thông tin nhân viên và người xác nhận / khóa.</summary>
public class TimesheetRow : MonthlyTimesheet
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PositionTitle { get; set; }
    public string? BranchName { get; set; }
    public string? ConfirmedByName { get; set; }
    public string? LockedByName { get; set; }
}

public interface ITimesheetRepository : IGenericRepository<MonthlyTimesheet, long>
{
    Task<PaginatedResult<TimesheetRow>> GetDetailsPagedAsync(TimesheetQuery query, CancellationToken cancellationToken = default);

    Task<TimesheetRow?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Bảng công đã lưu của các nhân viên trong tháng.</summary>
    Task<IReadOnlyList<MonthlyTimesheet>> GetByUsersAsync(int year, int month, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu số liệu mới tính. Chưa có bảng công thì tạo (nháp); đã có thì chỉ ghi đè khi vẫn là nháp — câu lệnh tự kiểm tra trạng thái nên
    /// an toàn khi Quản lý vừa xác nhận ở một phiên khác.
    /// </summary>
    Task UpsertDraftAsync(MonthlyTimesheet timesheet, CancellationToken cancellationToken = default);

    /// <summary>Nháp → đã xác nhận. false nếu bảng công không còn là nháp.</summary>
    Task<bool> TryConfirmAsync(long id, int confirmedBy, DateTime confirmedAt, CancellationToken cancellationToken = default);

    /// <summary>Khóa các bảng công đã xác nhận của tháng (theo chi nhánh nếu có; <paramref name="userIds"/> để khóa riêng một số người).</summary>
    /// <returns>Số bảng công vừa khóa.</returns>
    Task<int> LockConfirmedAsync(int year, int month, int? branchId, IReadOnlyCollection<int>? userIds, int lockedBy, DateTime lockedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Mở lại về nháp (xóa dấu xác nhận / khóa). false nếu bảng công không ở trạng thái <paramref name="fromStatus"/>.</summary>
    Task<bool> TryReopenAsync(long id, string fromStatus, string? note, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(int year, int month, int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Bảng công của nhân viên trong tháng đã khóa / chốt lương chưa.</summary>
    Task<bool> IsFrozenAsync(int userId, int year, int month, CancellationToken cancellationToken = default);
}
