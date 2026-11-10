using System.Data;

namespace FW.Infrastructure.Database;

/// <summary>
/// Giao diện khởi tạo kết nối cơ sở dữ liệu (Dapper / ADO.NET).
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

