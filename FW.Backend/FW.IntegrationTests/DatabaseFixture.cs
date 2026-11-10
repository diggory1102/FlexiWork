using FW.Infrastructure.Database;
using MySqlConnector;

namespace FW.IntegrationTests;

/// <summary>
/// Khởi tạo một CSDL kiểm thử RIÊNG từ flexiwork_schema.sql + flexiwork_seed.sql (chạy 1 lần cho cả bộ test).
///
/// Cách chạy:
///   PowerShell: $env:FW_TEST_DB = "Server=localhost;Port=3306;Database=flexiwork_test;User=root;Password=...;"
///   dotnet test
/// Nếu không đặt biến môi trường, các test tích hợp được bỏ qua (Skipped).
/// Tên CSDL bắt buộc khác "flexiwork_db" để tránh xóa nhầm dữ liệu phát triển.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "FW_TEST_DB";
    private const string ProductionDatabaseName = "flexiwork_db";

    public string ConnectionString { get; private set; } = string.Empty;
    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            SkipReason = $"Chưa đặt biến môi trường {EnvironmentVariable} — bỏ qua test tích hợp MySQL.";
            return;
        }

        var builder = new MySqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName) ||
            databaseName.Equals(ProductionDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariable} phải trỏ tới CSDL kiểm thử riêng (vd: flexiwork_test), không được là '{ProductionDatabaseName}'.");
        }

        // Kết nối ở mức server (chưa chọn database) để script tự tạo database kiểm thử
        builder.Database = string.Empty;
        await using (var connection = new MySqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            foreach (var script in new[] { "flexiwork_schema.sql", "flexiwork_seed.sql" })
            {
                var sql = File.ReadAllText(FindDatabaseScript(script)).Replace(ProductionDatabaseName, databaseName);
                await using var command = new MySqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
            }
        }

        ConnectionString = connectionString;
        IsAvailable = true;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Tạo phiên CSDL mới (tương đương một request).</summary>
    public DbSession CreateSession() => new(new MySqlDbConnectionFactory(ConnectionString));

    private static string FindDatabaseScript(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FW.Infrastructure", "Database", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Không tìm thấy FW.Infrastructure/Database/{fileName}");
    }
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "MySQL";
}
