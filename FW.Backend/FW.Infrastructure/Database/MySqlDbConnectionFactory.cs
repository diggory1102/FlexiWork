using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace FW.Infrastructure.Database;

/// <summary>
/// Khởi tạo kết nối MySQL 8.0 (MySqlConnector) và đặt múi giờ cho phiên làm việc.
/// </summary>
public class MySqlDbConnectionFactory : IDbConnectionFactory
{
    /// <summary>Múi giờ mặc định của hệ thống: giờ Việt Nam.</summary>
    public const string DefaultTimeZone = "+07:00";

    private readonly string _connectionString;
    private readonly string _setTimeZoneSql;

    public MySqlDbConnectionFactory(IConfiguration configuration)
        : this(
            configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
            configuration["Database:TimeZone"] ?? DefaultTimeZone)
    {
    }

    public MySqlDbConnectionFactory(string connectionString, string timeZone = DefaultTimeZone)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình chuỗi kết nối 'ConnectionStrings:DefaultConnection' " +
                "(xem appsettings.Development.example.json hoặc đặt biến môi trường ConnectionStrings__DefaultConnection).");
        }
        if (!Regex.IsMatch(timeZone, @"^[+-]\d{2}:\d{2}$"))
        {
            throw new InvalidOperationException($"Database:TimeZone '{timeZone}' không hợp lệ, cần dạng +07:00.");
        }

        _connectionString = connectionString;
        _setTimeZoneSql = $"SET time_zone = '{timeZone}';";
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Connection pool reset trạng thái phiên mỗi lần tái sử dụng, nên phải đặt lại time_zone sau mỗi lần mở.
        // Nhờ vậy CURRENT_TIMESTAMP / NOW() luôn trả giờ Việt Nam dù MySQL server (Aiven, Docker) chạy UTC.
        await using var command = connection.CreateCommand();
        command.CommandText = _setTimeZoneSql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
