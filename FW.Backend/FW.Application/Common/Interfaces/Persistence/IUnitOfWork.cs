namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Quản lý transaction dùng chung cho mọi repository trong cùng một request (scoped).
/// Dùng khi một nghiệp vụ cập nhật nhiều bảng, ví dụ duyệt đơn: leave_requests + leave_balances + schedules.
/// </summary>
public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>Chạy <paramref name="action"/> trong transaction: thành công thì commit, lỗi thì rollback và ném lại ngoại lệ.</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default);
}
