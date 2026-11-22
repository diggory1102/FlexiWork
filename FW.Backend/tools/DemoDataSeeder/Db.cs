using System.Reflection;
using System.Text;
using Dapper;
using FW.Domain.Common;
using FW.Infrastructure.Database;

namespace DemoDataSeeder;

/// <summary>Truy cập CSDL của công cụ: dùng chung một kết nối / transaction của <see cref="DbSession"/>, sinh INSERT theo quy ước của EntityMap.</summary>
internal sealed class Db
{
    private const int BatchSize = 300;

    private readonly DbSession _session;

    public Db(DbSession session)
    {
        _session = session;
    }

    public async Task<int> ExecAsync(string sql, object? parameters = null)
    {
        var connection = await _session.GetConnectionAsync();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, _session.Transaction));
    }

    public async Task<List<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        var connection = await _session.GetConnectionAsync();
        return (await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, _session.Transaction))).ToList();
    }

    public async Task<T?> ScalarAsync<T>(string sql, object? parameters = null)
    {
        var connection = await _session.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, _session.Transaction));
    }

    /// <summary>Thêm một dòng, trả về Id tự tăng.</summary>
    public async Task<TKey> InsertAsync<T, TKey>(string table, T entity) where T : BaseEntity<TKey>
    {
        var map = EntityMap.For<T>();
        var connection = await _session.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<TKey>(new CommandDefinition(
            $"INSERT INTO `{table}` ({map.InsertColumns}) VALUES ({map.InsertValues}); SELECT LAST_INSERT_ID();", entity, _session.Transaction))!;
    }

    /// <summary>Thêm nhiều dòng bằng các câu INSERT nhiều giá trị (không trả Id) — nhanh hơn nhiều khi chạy qua mạng tới TiDB Cloud.</summary>
    public async Task<int> InsertManyAsync<T>(string table, IReadOnlyList<T> rows)
    {
        if (rows.Count == 0) return 0;

        var map = EntityMap.For<T>();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name != "Id" && p.Name != nameof(IAuditableEntity.CreatedAt) && p.Name != nameof(IAuditableEntity.UpdatedAt)
                        && p.Name != nameof(ISoftDelete.IsDeleted))
            .ToList();

        var inserted = 0;
        foreach (var batch in rows.Chunk(BatchSize))
        {
            var sql = new StringBuilder($"INSERT INTO `{table}` ({map.InsertColumns}) VALUES ");
            var parameters = new DynamicParameters();
            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sql.Append(", ");
                sql.Append('(').Append(string.Join(", ", properties.Select((p, j) => $"@p{i}_{j}"))).Append(')');
                for (var j = 0; j < properties.Count; j++) parameters.Add($"p{i}_{j}", properties[j].GetValue(batch[i]));
            }
            inserted += await ExecAsync(sql.ToString(), parameters);
        }
        return inserted;
    }
}
