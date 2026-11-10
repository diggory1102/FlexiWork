using FW.Application.Common.Models;
using FW.Domain.Common;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Repository CRUD chung. Với Entity có <see cref="ISoftDelete"/>, mọi truy vấn tự loại bản ghi đã xóa
/// và <see cref="DeleteAsync"/> chỉ đánh dấu is_deleted = 1.
/// </summary>
public interface IGenericRepository<T, TKey> where T : BaseEntity<TKey>
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PaginatedResult<T>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);

    /// <returns>Id vừa được CSDL cấp.</returns>
    Task<TKey> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <returns>false nếu không tìm thấy bản ghi (hoặc đã bị xóa).</returns>
    Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <returns>false nếu không tìm thấy bản ghi (hoặc đã bị xóa).</returns>
    Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}

public interface IGenericRepository<T> : IGenericRepository<T, int> where T : BaseEntity<int>
{
}
