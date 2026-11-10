using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public interface IDepartmentRepository : IGenericRepository<Department>
{
    /// <summary>Kiểm tra trùng mã phòng ban trên tất cả bản ghi (cột code có ràng buộc UNIQUE).</summary>
    Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IPositionRepository : IGenericRepository<Position>
{
    /// <summary>Kiểm tra trùng tên chức danh (không phân biệt hoa thường, bỏ qua bản ghi đã xóa).</summary>
    Task<bool> ExistsByTitleAsync(string title, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetByNamesAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken = default);
}
