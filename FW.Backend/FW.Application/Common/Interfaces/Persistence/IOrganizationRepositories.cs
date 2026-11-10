using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public interface IDepartmentRepository : IGenericRepository<Department>
{
    /// <summary>Kiểm tra trùng mã phòng ban trên tất cả bản ghi (cột code có ràng buộc UNIQUE).</summary>
    Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IPositionRepository : IGenericRepository<Position>
{
}

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetByNamesAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken = default);
}
