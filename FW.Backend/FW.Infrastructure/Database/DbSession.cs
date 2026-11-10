using System.Data.Common;
using FW.Application.Common.Interfaces.Persistence;

namespace FW.Infrastructure.Database;

/// <summary>
/// Phiên làm việc CSDL theo request (scoped): giữ một kết nối duy nhất và transaction hiện hành
/// để mọi repository trong cùng request dùng chung. Đồng thời là cài đặt của <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class DbSession : IUnitOfWork, IAsyncDisposable, IDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private DbConnection? _connection;

    public DbSession(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public DbTransaction? Transaction { get; private set; }

    public bool HasActiveTransaction => Transaction != null;

    /// <summary>Lấy kết nối của phiên (mở ở lần gọi đầu tiên).</summary>
    public async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        _connection ??= await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return _connection;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Transaction != null)
        {
            throw new InvalidOperationException("Đã có transaction đang mở trong phiên này.");
        }

        var connection = await GetConnectionAsync(cancellationToken);
        Transaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (Transaction == null)
        {
            throw new InvalidOperationException("Không có transaction nào để commit.");
        }

        await Transaction.CommitAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (Transaction == null) return;

        await Transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        // Lồng nhau: nếu đã ở trong transaction thì chạy tiếp trong transaction đó, để lớp ngoài quyết định commit.
        if (HasActiveTransaction)
        {
            return await action(cancellationToken);
        }

        await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            // Không truyền cancellationToken: vẫn phải rollback kể cả khi request bị hủy.
            await RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (Transaction != null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public void Dispose()
    {
        Transaction?.Dispose();
        Transaction = null;
        _connection?.Dispose();
        _connection = null;
    }
}
