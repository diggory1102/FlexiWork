using System.Data.Common;

namespace FW.Infrastructure.Database;

/// <summary>
/// Khởi tạo kết nối cơ sở dữ liệu đã mở sẵn (Dapper / ADO.NET).
/// Repository không gọi trực tiếp mà lấy kết nối qua <see cref="DbSession"/> để dùng chung transaction.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
