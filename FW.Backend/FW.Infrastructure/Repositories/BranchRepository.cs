using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class BranchRepository : BaseRepository<Branch>, IBranchRepository
{
    public BranchRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "branches";
    protected override IReadOnlyList<string> SearchColumns => new[] { "`branch_code`", "`name`", "`address`" };
    protected override string DefaultOrderBy => "`branch_code` ASC";

    public Task<Branch?> GetByCodeAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE `branch_code` = @BranchCode AND {NotDeletedCondition}";
        return QuerySingleOrDefaultAsync<Branch>(sql, new { BranchCode = branchCode }, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string branchCode, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `branches`
                          WHERE `branch_code` = @BranchCode AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { BranchCode = branchCode, ExcludeId = excludeId }, cancellationToken);
    }
}

public class BranchWifiRepository : BaseRepository<BranchWifi>, IBranchWifiRepository
{
    public BranchWifiRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "branch_wifis";
    protected override IReadOnlyList<string> SearchColumns => new[] { "`ssid`", "`bssid`" };

    public Task<IReadOnlyList<BranchWifi>> GetByBranchIdAsync(int branchId, bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectFrom} WHERE `branch_id` = @BranchId AND {NotDeletedCondition}"
                  + (activeOnly ? " AND `is_active` = 1" : string.Empty)
                  + " ORDER BY `ssid`";
        return QueryAsync<BranchWifi>(sql, new { BranchId = branchId }, cancellationToken);
    }

    public Task<bool> ExistsBssidAsync(int branchId, string bssid, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM `branch_wifis`
                          WHERE `branch_id` = @BranchId AND `bssid` = @Bssid AND `is_deleted` = 0
                            AND (@ExcludeId IS NULL OR `id` <> @ExcludeId))
            """;
        return ExecuteScalarAsync<bool>(sql, new { BranchId = branchId, Bssid = bssid, ExcludeId = excludeId }, cancellationToken);
    }
}
