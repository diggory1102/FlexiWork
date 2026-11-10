using System.Data;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace FW.Infrastructure.Database;

/// <summary>
/// Khởi tạo kết nối MySQL 8.0 sử dụng thư viện MySqlConnector tối ưu hiệu năng cho Dapper.
/// </summary>
public class MySqlDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Chuỗi kết nối 'DefaultConnection' không được tìm thấy trong cấu hình appsettings.json.");
    }

    public IDbConnection CreateConnection()
    {
        var connection = new MySqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

