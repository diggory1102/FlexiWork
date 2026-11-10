using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Bộ lọc danh sách nhân sự. Search tìm trên mã nhân viên, email, họ tên, số điện thoại.
/// </summary>
public class UserFilter : PagedQuery
{
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public string? Status { get; set; }

    /// <summary>Lọc theo tên vai trò (giá trị thuộc RoleNames).</summary>
    public string? RoleName { get; set; }
}

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<PaginatedResult<User>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra trùng email trên tất cả bản ghi (cột email có ràng buộc UNIQUE).</summary>
    Task<bool> ExistsByEmailAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra trùng mã nhân viên trên tất cả bản ghi (cột employee_code có ràng buộc UNIQUE).</summary>
    Task<bool> ExistsByEmployeeCodeAsync(string employeeCode, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Lấy vai trò của nhiều người dùng trong một truy vấn (tránh N+1 khi hiển thị danh sách).</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetRoleNamesByUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>Thay thế toàn bộ vai trò của người dùng (chạy trong transaction).</summary>
    Task SetRolesAsync(int userId, IEnumerable<int> roleIds, CancellationToken cancellationToken = default);
}
