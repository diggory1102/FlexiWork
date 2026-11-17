using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public interface IAllowanceRepository : IGenericRepository<Allowance>
{
    /// <summary>Danh mục phụ cấp (kèm số nhân viên được gán riêng); <paramref name="activeOnly"/> = chỉ phụ cấp đang áp dụng.</summary>
    Task<IReadOnlyList<AllowanceRow>> GetListAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<AllowanceRow?> GetRowAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Số nhân viên được gán riêng phụ cấp này.</summary>
    Task<int> CountAssignedAsync(int allowanceId, CancellationToken cancellationToken = default);
}

public class AllowanceRow : Allowance
{
    public int AssignedCount { get; set; }
}

public interface IUserAllowanceRepository
{
    Task<IReadOnlyList<UserAllowance>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserAllowance>> GetByUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>Thay toàn bộ phụ cấp gán riêng của nhân viên (gọi trong transaction).</summary>
    Task ReplaceForUserAsync(int userId, IReadOnlyCollection<UserAllowance> items, CancellationToken cancellationToken = default);
}

/// <summary>Lọc danh sách cấu hình lương theo nhân viên.</summary>
public class PayrollEmployeeQuery : PagedQuery
{
    public int? BranchId { get; set; }

    /// <summary>full_time / part_time.</summary>
    public string? EmploymentType { get; set; }

    /// <summary>true: chỉ người đã đủ thông tin tính lương; false: chỉ người còn thiếu; null: tất cả.</summary>
    public bool? Configured { get; set; }
}

/// <summary>Cấu hình lương của một nhân viên kèm thông tin hiển thị.</summary>
public class PayrollEmployeeRow
{
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? PositionTitle { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public decimal? BaseSalary { get; set; }
    public decimal? HourlyRate { get; set; }

    /// <summary>Đơn giá giờ theo chức vụ.</summary>
    public decimal? PositionHourlyRate { get; set; }

    /// <summary>Số phụ cấp đang hưởng (áp dụng cho tất cả + được gán riêng).</summary>
    public int AllowanceCount { get; set; }
}

public interface IPayrollConfigRepository
{
    Task<PaginatedResult<PayrollEmployeeRow>> GetEmployeesPagedAsync(PayrollEmployeeQuery query, CancellationToken cancellationToken = default);

    Task<PayrollEmployeeRow?> GetEmployeeAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Cấu hình lương của nhiều nhân viên (tính bảng lương).</summary>
    Task<IReadOnlyList<PayrollEmployeeRow>> GetEmployeesByIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>Ghi lương cơ bản và đơn giá giờ riêng (null = xóa). false nếu nhân viên không tồn tại.</summary>
    Task<bool> UpdatePayAsync(int userId, decimal? baseSalary, decimal? hourlyRate, CancellationToken cancellationToken = default);
}
