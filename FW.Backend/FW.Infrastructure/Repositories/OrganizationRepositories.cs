using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class DepartmentRepository : BaseRepository<Department>, IDepartmentRepository
{
    public DepartmentRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "departments";
    protected override IReadOnlyList<string> SearchColumns => new[] { "`code`", "`name`" };
    protected override string DefaultOrderBy => "`code` ASC";

    public Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `departments`
                          WHERE `code` = @Code AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { Code = code, ExcludeId = excludeId }, cancellationToken);
    }
}

public class PositionRepository : BaseRepository<Position>, IPositionRepository
{
    public PositionRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "positions";
    protected override IReadOnlyList<string> SearchColumns => new[] { "`title`" };
    protected override string DefaultOrderBy => "`title` ASC";

    public Task<bool> ExistsByTitleAsync(string title, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        // Collation utf8mb4_unicode_ci đã so sánh không phân biệt hoa thường
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `positions`
                          WHERE `title` = @Title AND `is_deleted` = 0 AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { Title = title.Trim(), ExcludeId = excludeId }, cancellationToken);
    }
}

public class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    public RoleRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "roles";
    protected override string DefaultOrderBy => "`id` ASC";

    public Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE `role_name` = @RoleName";
        return QuerySingleOrDefaultAsync<Role>(sql, new { RoleName = roleName }, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetByNamesAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        var names = roleNames.Distinct().ToArray();
        if (names.Length == 0) return Array.Empty<Role>();

        // Dapper tự mở rộng "IN @RoleNames" thành danh sách tham số
        var sql = $"{SelectFrom} WHERE `role_name` IN @RoleNames ORDER BY `id`";
        return await QueryAsync<Role>(sql, new { RoleNames = names }, cancellationToken);
    }
}
