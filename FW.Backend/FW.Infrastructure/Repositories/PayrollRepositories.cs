using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class AllowanceRepository : BaseRepository<Allowance>, IAllowanceRepository
{
    public AllowanceRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "allowances";
    protected override string DefaultOrderBy => "`id`";

    private const string RowSelect = """
        SELECT a.*, (SELECT COUNT(*) FROM `user_allowances` ua WHERE ua.`allowance_id` = a.`id`) AS AssignedCount
        FROM `allowances` a
        """;

    public Task<IReadOnlyList<AllowanceRow>> GetListAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        QueryAsync<AllowanceRow>($"{RowSelect} WHERE a.`is_deleted` = 0{(activeOnly ? " AND a.`is_active` = 1" : string.Empty)} ORDER BY a.`id`",
            null, cancellationToken);

    public Task<AllowanceRow?> GetRowAsync(int id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<AllowanceRow>($"{RowSelect} WHERE a.`is_deleted` = 0 AND a.`id` = @Id", new { Id = id }, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM `allowances` WHERE `name` = @Name AND `is_deleted` = 0 AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """, new { Name = name, ExcludeId = excludeId }, cancellationToken);

    public Task<int> CountAssignedAsync(int allowanceId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM `user_allowances` WHERE `allowance_id` = @Id", new { Id = allowanceId }, cancellationToken);
}

public class UserAllowanceRepository : BaseRepository<UserAllowance>, IUserAllowanceRepository
{
    public UserAllowanceRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "user_allowances";

    public Task<IReadOnlyList<UserAllowance>> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        QueryAsync<UserAllowance>($"{SelectFrom} WHERE `user_id` = @UserId ORDER BY `allowance_id`", new { UserId = userId }, cancellationToken);

    public async Task ReplaceForUserAsync(int userId, IReadOnlyCollection<UserAllowance> items, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("DELETE FROM `user_allowances` WHERE `user_id` = @UserId", new { UserId = userId }, cancellationToken);
        foreach (var item in items)
        {
            item.UserId = userId;
            await AddAsync(item, cancellationToken);
        }
    }
}

public class PayrollConfigRepository : BaseRepository<User>, IPayrollConfigRepository
{
    public PayrollConfigRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "users";

    private const string RowSelect = """
        SELECT u.`id` AS UserId, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`avatar_url` AS AvatarUrl, u.`status` AS Status,
               u.`branch_id` AS BranchId, b.`name` AS BranchName, p.`title` AS PositionTitle, u.`employment_type` AS EmploymentType,
               u.`base_salary` AS BaseSalary, u.`hourly_rate` AS HourlyRate, p.`base_salary_per_hour` AS PositionHourlyRate,
               (SELECT COUNT(*) FROM `allowances` a
                WHERE a.`is_deleted` = 0 AND a.`is_active` = 1
                  AND (a.`apply_to_all` = 1 OR EXISTS(SELECT 1 FROM `user_allowances` ua WHERE ua.`allowance_id` = a.`id` AND ua.`user_id` = u.`id`))
               ) AS AllowanceCount
        FROM `users` u
        LEFT JOIN `positions` p ON p.`id` = u.`position_id`
        LEFT JOIN `branches` b ON b.`id` = u.`branch_id`
        """;

    // Đã đủ thông tin tính lương: bán thời gian cần đơn giá giờ (riêng hoặc theo chức vụ); toàn thời gian cần lương tháng
    private const string ConfiguredExpression =
        "(CASE WHEN u.`employment_type` = 'part_time' THEN COALESCE(u.`hourly_rate`, p.`base_salary_per_hour`, 0) > 0 ELSE COALESCE(u.`base_salary`, 0) > 0 END)";

    public async Task<PaginatedResult<PayrollEmployeeRow>> GetEmployeesPagedAsync(PayrollEmployeeQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "u.`is_deleted` = 0" };
        var parameters = new DynamicParameters();
        if (query.BranchId.HasValue)
        {
            conditions.Add("u.`branch_id` = @BranchId");
            parameters.Add("BranchId", query.BranchId);
        }
        if (!string.IsNullOrWhiteSpace(query.EmploymentType))
        {
            conditions.Add("u.`employment_type` = @EmploymentType");
            parameters.Add("EmploymentType", query.EmploymentType);
        }
        if (query.Configured.HasValue)
        {
            conditions.Add(query.Configured.Value ? ConfiguredExpression : $"NOT {ConfiguredExpression}");
        }
        AddSearchCondition(query.Search, new[] { "u.`full_name`", "u.`employee_code`" }, conditions, parameters);
        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT COUNT(*) FROM `users` u LEFT JOIN `positions` p ON p.`id` = u.`position_id` WHERE {where};
            {RowSelect} WHERE {where} ORDER BY b.`name`, u.`employee_code` LIMIT @Take OFFSET @Skip;
            """;
        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<PayrollEmployeeRow>()).ToList();
        return PaginatedResult<PayrollEmployeeRow>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    public Task<PayrollEmployeeRow?> GetEmployeeAsync(int userId, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<PayrollEmployeeRow>($"{RowSelect} WHERE u.`is_deleted` = 0 AND u.`id` = @Id", new { Id = userId }, cancellationToken);

    public async Task<bool> UpdatePayAsync(int userId, decimal? baseSalary, decimal? hourlyRate, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("""
            UPDATE `users` SET `base_salary` = @BaseSalary, `hourly_rate` = @HourlyRate WHERE `id` = @Id AND `is_deleted` = 0
            """, new { Id = userId, BaseSalary = baseSalary, HourlyRate = hourlyRate }, cancellationToken) > 0;
}
