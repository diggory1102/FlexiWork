using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class ShiftRepository : BaseRepository<Shift>, IShiftRepository
{
    public ShiftRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "shifts";
    protected override IReadOnlyList<string> SearchColumns => new[] { "`shift_code`", "`name`" };
    protected override string DefaultOrderBy => "`start_time` ASC, `shift_code` ASC";

    public Task<PaginatedResult<Shift>> GetPagedAsync(ShiftFilter filter, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddSearchCondition(filter.Search, SearchColumns, conditions, parameters);

        if (filter.BranchId.HasValue)
        {
            conditions.Add(filter.IncludeShared
                ? "(`branch_id` = @BranchId OR `branch_id` IS NULL)"
                : "`branch_id` = @BranchId");
            parameters.Add("BranchId", filter.BranchId);
        }

        return QueryPagedAsync(filter, conditions, parameters, cancellationToken);
    }

    public override Task<PaginatedResult<Shift>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default) =>
        query is ShiftFilter filter
            ? GetPagedAsync(filter, cancellationToken)
            : GetPagedAsync(new ShiftFilter { PageIndex = query.PageIndex, PageSize = query.PageSize, Search = query.Search }, cancellationToken);

    public Task<IReadOnlyList<Shift>> GetAvailableForBranchAsync(int branchId, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE (`branch_id` = @BranchId OR `branch_id` IS NULL) AND {NotDeletedCondition} ORDER BY {DefaultOrderBy}";
        return QueryAsync<Shift>(sql, new { BranchId = branchId }, cancellationToken);
    }

    public async Task<IReadOnlyList<Shift>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var distinct = ids.Distinct().ToArray();
        if (distinct.Length == 0) return Array.Empty<Shift>();

        var sql = $"{SelectFrom} WHERE `id` IN @Ids AND {NotDeletedCondition}";
        return await QueryAsync<Shift>(sql, new { Ids = distinct }, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string shiftCode, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `shifts`
                          WHERE `shift_code` = @ShiftCode AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { ShiftCode = shiftCode, ExcludeId = excludeId }, cancellationToken);
    }

    public Task<bool> IsInUseAsync(int shiftId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM `schedules` WHERE `shift_id` = @ShiftId)",
            new { ShiftId = shiftId }, cancellationToken);
}
