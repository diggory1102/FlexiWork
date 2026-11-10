namespace FW.Domain.Interfaces;

/// <summary>
/// Giao diện chuẩn cho Repository thao tác dữ liệu CRUD cơ bản.
/// </summary>
public interface IGenericRepository<T, TKey> where T : class
{
    Task<T?> GetByIdAsync(TKey id);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<TKey> AddAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(TKey id);
}

public interface IGenericRepository<T> : IGenericRepository<T, int> where T : class
{
}

