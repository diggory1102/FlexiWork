using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Services;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class ShiftStaffingRuleRepository : BaseRepository<ShiftStaffingRule>, IShiftStaffingRuleRepository
{
    public ShiftStaffingRuleRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "shift_staffing_rules";
    protected override string DefaultOrderBy => "`scope_key` ASC, `shift_id` ASC";

    public Task<IReadOnlyList<ShiftStaffingRule>> GetForRangeAsync(int branchId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        // scope_key dạng "date:yyyy-MM-dd" so sánh theo thứ tự chữ chính là thứ tự ngày
        var sql = $"""
            {SelectFrom}
            WHERE `branch_id` = @BranchId
              AND (`scope_key` LIKE @WeekdayLike OR `scope_key` BETWEEN @FromKey AND @ToKey)
            ORDER BY {DefaultOrderBy}
            """;
        return QueryAsync<ShiftStaffingRule>(sql, new
        {
            BranchId = branchId,
            WeekdayLike = ShiftStaffing.WeekdayPrefix + "%",
            FromKey = ShiftStaffing.DateKey(fromDate),
            ToKey = ShiftStaffing.DateKey(toDate)
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ShiftStaffingRule>> GetUpcomingAsync(int branchId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            {SelectFrom}
            WHERE `branch_id` = @BranchId
              AND (`scope_key` LIKE @WeekdayLike OR `scope_key` >= @FromKey)
            ORDER BY {DefaultOrderBy}
            """;
        return QueryAsync<ShiftStaffingRule>(sql, new
        {
            BranchId = branchId,
            WeekdayLike = ShiftStaffing.WeekdayPrefix + "%",
            FromKey = ShiftStaffing.DateKey(fromDate)
        }, cancellationToken);
    }

    public Task<ShiftStaffingRule?> GetByKeyAsync(int branchId, int shiftId, string scopeKey, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<ShiftStaffingRule>(
            $"{SelectFrom} WHERE `branch_id` = @BranchId AND `shift_id` = @ShiftId AND `scope_key` = @ScopeKey",
            new { BranchId = branchId, ShiftId = shiftId, ScopeKey = scopeKey }, cancellationToken);

    public async Task<int> UpsertAsync(ShiftStaffingRule rule, CancellationToken cancellationToken = default)
    {
        // UNIQUE (branch_id, shift_id, scope_key): hai người cùng đặt ngoại lệ cho một ô thì bản sau thay bản trước, không sinh hai dòng
        const string sql = """
            INSERT INTO `shift_staffing_rules` (`branch_id`, `shift_id`, `scope_key`, `min_staff`, `max_staff`, `note`, `created_by`)
            VALUES (@BranchId, @ShiftId, @ScopeKey, @MinStaff, @MaxStaff, @Note, @CreatedBy)
            ON DUPLICATE KEY UPDATE
                `min_staff` = VALUES(`min_staff`),
                `max_staff` = VALUES(`max_staff`),
                `note` = VALUES(`note`),
                `created_by` = VALUES(`created_by`);
            SELECT `id` FROM `shift_staffing_rules` WHERE `branch_id` = @BranchId AND `shift_id` = @ShiftId AND `scope_key` = @ScopeKey;
            """;
        var id = await ExecuteScalarAsync<int>(sql, rule, cancellationToken);
        rule.Id = id;
        return id;
    }
}
