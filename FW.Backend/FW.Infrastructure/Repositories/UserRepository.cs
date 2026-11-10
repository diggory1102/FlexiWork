using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "users";
    protected override IReadOnlyList<string> SearchColumns =>
        new[] { "`employee_code`", "`email`", "`full_name`", "`phone_number`" };
    protected override string DefaultOrderBy => "`employee_code` ASC";

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE `email` = @Email AND {NotDeletedCondition}";
        return QuerySingleOrDefaultAsync<User>(sql, new { Email = email.Trim() }, cancellationToken);
    }

    public Task<PaginatedResult<User>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddSearchCondition(filter.Search, SearchColumns, conditions, parameters);

        if (filter.BranchId.HasValue)
        {
            conditions.Add("`branch_id` = @BranchId");
            parameters.Add("BranchId", filter.BranchId);
        }
        if (filter.DepartmentId.HasValue)
        {
            conditions.Add("`department_id` = @DepartmentId");
            parameters.Add("DepartmentId", filter.DepartmentId);
        }
        if (filter.PositionId.HasValue)
        {
            conditions.Add("`position_id` = @PositionId");
            parameters.Add("PositionId", filter.PositionId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("`status` = @Status");
            parameters.Add("Status", filter.Status);
        }
        if (!string.IsNullOrWhiteSpace(filter.RoleName))
        {
            conditions.Add("""
                EXISTS(SELECT 1 FROM `user_roles` ur JOIN `roles` r ON r.`id` = ur.`role_id`
                       WHERE ur.`user_id` = `users`.`id` AND r.`role_name` = @RoleName)
                """);
            parameters.Add("RoleName", filter.RoleName);
        }

        return QueryPagedAsync(filter, conditions, parameters, cancellationToken);
    }

    public override Task<PaginatedResult<User>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default) =>
        query is UserFilter filter
            ? GetPagedAsync(filter, cancellationToken)
            : GetPagedAsync(new UserFilter { PageIndex = query.PageIndex, PageSize = query.PageSize, Search = query.Search }, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `users` WHERE `email` = @Email AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { Email = email.Trim(), ExcludeId = excludeId }, cancellationToken);
    }

    public Task<bool> ExistsByEmployeeCodeAsync(string employeeCode, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `users` WHERE `employee_code` = @EmployeeCode AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { EmployeeCode = employeeCode.Trim(), ExcludeId = excludeId }, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT r.`role_name` FROM `user_roles` ur JOIN `roles` r ON r.`id` = ur.`role_id`
            WHERE ur.`user_id` = @UserId ORDER BY r.`id`
            """;
        return QueryAsync<string>(sql, new { UserId = userId }, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetRoleNamesByUserIdsAsync(
        IEnumerable<int> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, IReadOnlyList<string>>();

        const string sql = """
            SELECT ur.`user_id` AS UserId, r.`role_name` AS RoleName
            FROM `user_roles` ur JOIN `roles` r ON r.`id` = ur.`role_id`
            WHERE ur.`user_id` IN @UserIds ORDER BY ur.`user_id`, r.`id`
            """;
        var rows = await QueryAsync<(int UserId, string RoleName)>(sql, new { UserIds = ids }, cancellationToken);

        return ids.ToDictionary(
            id => id,
            id => (IReadOnlyList<string>)rows.Where(r => r.UserId == id).Select(r => r.RoleName).ToList());
    }

    public Task SetRolesAsync(int userId, IEnumerable<int> roleIds, CancellationToken cancellationToken = default)
    {
        var rows = roleIds.Distinct().Select(roleId => new { UserId = userId, RoleId = roleId }).ToList();

        // Xóa rồi thêm lại phải nằm trong cùng transaction để không bao giờ có trạng thái "mất hết vai trò"
        return Session.ExecuteInTransactionAsync(async ct =>
        {
            await ExecuteAsync("DELETE FROM `user_roles` WHERE `user_id` = @UserId", new { UserId = userId }, ct);
            if (rows.Count > 0)
            {
                // Dapper thực thi câu lệnh một lần cho mỗi phần tử trong danh sách
                await ExecuteAsync("INSERT INTO `user_roles` (`user_id`, `role_id`) VALUES (@UserId, @RoleId)", rows, ct);
            }
        }, cancellationToken);
    }
}
