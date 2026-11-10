using System.Data.Common;
using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Common;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

/// <summary>
/// Repository cơ sở dùng Dapper: tự sinh câu lệnh CRUD từ <see cref="EntityMap"/>.
/// Lớp con chỉ cần khai báo <see cref="TableName"/>, cột tìm kiếm và các truy vấn đặc thù.
/// Tên bảng/cột lấy từ mã nguồn; mọi giá trị đều truyền qua tham số nên không có nguy cơ SQL injection.
/// </summary>
public abstract class BaseRepository<T, TKey> : IGenericRepository<T, TKey> where T : BaseEntity<TKey>
{
    protected static readonly EntityMap Map = EntityMap.For<T>();

    protected BaseRepository(DbSession session)
    {
        Session = session;
    }

    protected DbSession Session { get; }

    /// <summary>Tên bảng trong CSDL.</summary>
    protected abstract string TableName { get; }

    /// <summary>Các cột áp dụng tìm kiếm gần đúng (LIKE) trong <see cref="GetPagedAsync"/>.</summary>
    protected virtual IReadOnlyList<string> SearchColumns => Array.Empty<string>();

    /// <summary>Mệnh đề sắp xếp mặc định (không gồm từ khóa ORDER BY).</summary>
    protected virtual string DefaultOrderBy => "`id` DESC";

    /// <summary>Điều kiện loại bản ghi đã xóa mềm (dùng cho truy vấn không có alias bảng).</summary>
    protected string NotDeletedCondition => Map.HasSoftDelete ? "`is_deleted` = 0" : "1 = 1";

    protected string SelectFrom => $"SELECT {Map.SelectColumns} FROM {EntityMap.Quote(TableName)}";

    public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE `id` = @Id AND {NotDeletedCondition}";
        return await QuerySingleOrDefaultAsync<T>(sql, new { Id = id }, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE {NotDeletedCondition} ORDER BY {DefaultOrderBy}";
        return await QueryAsync<T>(sql, null, cancellationToken);
    }

    public virtual Task<PaginatedResult<T>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();
        AddSearchCondition(query.Search, SearchColumns, conditions, parameters);
        return QueryPagedAsync(query, conditions, parameters, cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT EXISTS(SELECT 1 FROM {EntityMap.Quote(TableName)} WHERE `id` = @Id AND {NotDeletedCondition})";
        return await ExecuteScalarAsync<bool>(sql, new { Id = id }, cancellationToken);
    }

    public virtual async Task<TKey> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var sql = $"INSERT INTO {EntityMap.Quote(TableName)} ({Map.InsertColumns}) VALUES ({Map.InsertValues}); SELECT LAST_INSERT_ID();";
        var newId = await ExecuteScalarAsync<long>(sql, entity, cancellationToken);

        var id = (TKey)Convert.ChangeType(newId, typeof(TKey));
        entity.Id = id;
        return id;
    }

    public virtual async Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var sql = $"UPDATE {EntityMap.Quote(TableName)} SET {Map.UpdateSet} WHERE `id` = @Id AND {NotDeletedCondition}";
        return await ExecuteAsync(sql, entity, cancellationToken) > 0;
    }

    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var sql = Map.HasSoftDelete
            ? $"UPDATE {EntityMap.Quote(TableName)} SET `is_deleted` = 1 WHERE `id` = @Id AND `is_deleted` = 0"
            : $"DELETE FROM {EntityMap.Quote(TableName)} WHERE `id` = @Id";
        return await ExecuteAsync(sql, new { Id = id }, cancellationToken) > 0;
    }

    // ------------------------------------------------------------------
    // Hàm hỗ trợ cho lớp con
    // ------------------------------------------------------------------

    /// <summary>
    /// Truy vấn phân trang trên bảng của repository với các điều kiện bổ sung (đã viết sẵn tham số).
    /// Tự thêm điều kiện loại bản ghi đã xóa mềm.
    /// </summary>
    protected async Task<PaginatedResult<T>> QueryPagedAsync(
        PagedQuery query, List<string> conditions, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        var allConditions = new List<string> { NotDeletedCondition };
        allConditions.AddRange(conditions);
        var where = string.Join(" AND ", allConditions);

        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);

        var sql = $"""
            SELECT COUNT(*) FROM {EntityMap.Quote(TableName)} WHERE {where};
            {SelectFrom} WHERE {where} ORDER BY {DefaultOrderBy} LIMIT @Take OFFSET @Skip;
            """;

        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<T>()).ToList();

        return PaginatedResult<T>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    /// <summary>Thêm điều kiện (col1 LIKE @Search OR col2 LIKE @Search ...) nếu có từ khóa.</summary>
    protected static void AddSearchCondition(
        string? search, IReadOnlyList<string> columns, List<string> conditions, DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(search) || columns.Count == 0) return;

        conditions.Add("(" + string.Join(" OR ", columns.Select(c => $"{c} LIKE @Search")) + ")");
        parameters.Add("Search", $"%{EscapeLike(search.Trim())}%");
    }

    /// <summary>Thoát các ký tự đặc biệt của LIKE để từ khóa được tìm đúng nguyên văn.</summary>
    protected static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    protected CommandDefinition Command(string sql, object? parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, Session.Transaction, cancellationToken: cancellationToken);

    protected async Task<IReadOnlyList<TResult>> QueryAsync<TResult>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        var connection = await Session.GetConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<TResult>(Command(sql, parameters, cancellationToken))).ToList();
    }

    protected async Task<TResult?> QuerySingleOrDefaultAsync<TResult>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        var connection = await Session.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<TResult>(Command(sql, parameters, cancellationToken));
    }

    protected async Task<TResult> ExecuteScalarAsync<TResult>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        var connection = await Session.GetConnectionAsync(cancellationToken);
        return (await connection.ExecuteScalarAsync<TResult>(Command(sql, parameters, cancellationToken)))!;
    }

    protected async Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken cancellationToken)
    {
        var connection = await Session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(Command(sql, parameters, cancellationToken));
    }
}

/// <summary>Repository cơ sở cho Entity có khóa chính kiểu int.</summary>
public abstract class BaseRepository<T> : BaseRepository<T, int>, IGenericRepository<T> where T : BaseEntity<int>
{
    protected BaseRepository(DbSession session) : base(session)
    {
    }
}
