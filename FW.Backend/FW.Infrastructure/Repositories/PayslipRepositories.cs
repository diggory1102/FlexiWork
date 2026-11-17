using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class PayrollPeriodRepository : BaseRepository<PayrollPeriod>, IPayrollPeriodRepository
{
    public PayrollPeriodRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "payroll_periods";

    private const string RowSelect = """
        SELECT p.*, cu.`full_name` AS CreatedByName, fu.`full_name` AS FinalizedByName,
               (SELECT COUNT(*) FROM `payslips` s WHERE s.`period_id` = p.`id`) AS PayslipCount,
               (SELECT COUNT(*) FROM `payslips` s WHERE s.`period_id` = p.`id` AND s.`status` = 'sent') AS SentCount
        FROM `payroll_periods` p
        LEFT JOIN `users` cu ON cu.`id` = p.`created_by`
        LEFT JOIN `users` fu ON fu.`id` = p.`finalized_by`
        """;

    public Task<PayrollPeriodRow?> GetRowAsync(int id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<PayrollPeriodRow>($"{RowSelect} WHERE p.`id` = @Id", new { Id = id }, cancellationToken);

    public Task<PayrollPeriodRow?> GetRowByMonthAsync(int year, int month, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<PayrollPeriodRow>($"{RowSelect} WHERE p.`year` = @Year AND p.`month` = @Month", new { Year = year, Month = month }, cancellationToken);

    public Task<IReadOnlyList<PayrollPeriodRow>> GetRowsAsync(int? year, CancellationToken cancellationToken = default) =>
        QueryAsync<PayrollPeriodRow>($"{RowSelect} WHERE (@Year IS NULL OR p.`year` = @Year) ORDER BY p.`year` DESC, p.`month` DESC", new { Year = year }, cancellationToken);

    public async Task<int> EnsureAsync(int year, int month, int createdBy, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("""
            INSERT INTO `payroll_periods` (`month`, `year`, `created_by`) VALUES (@Month, @Year, @By)
            ON DUPLICATE KEY UPDATE `id` = `id`
            """, new { Month = month, Year = year, By = createdBy }, cancellationToken);
        return await ExecuteScalarAsync<int>("SELECT `id` FROM `payroll_periods` WHERE `year` = @Year AND `month` = @Month", new { Year = year, Month = month },
            cancellationToken);
    }

    public async Task<bool> TryUpdateCalculationAsync(int id, decimal standardWorkDays, decimal overtimeMultiplier, int employeeCount, decimal totalNet,
        DateTime calculatedAt, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("""
            UPDATE `payroll_periods`
            SET `standard_work_days` = @Std, `overtime_multiplier` = @Mult, `employee_count` = @Count, `total_net` = @Net, `calculated_at` = @At
            WHERE `id` = @Id AND `status` = @Draft
            """, new { Id = id, Std = standardWorkDays, Mult = overtimeMultiplier, Count = employeeCount, Net = totalNet, At = calculatedAt, Draft = PayrollPeriodStatus.Draft },
            cancellationToken) == 1;

    public async Task<bool> TryFinalizeAsync(int id, int finalizedBy, DateTime finalizedAt, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("""
            UPDATE `payroll_periods` SET `status` = @Finalized, `finalized_by` = @By, `finalized_at` = @At WHERE `id` = @Id AND `status` = @Draft
            """, new { Id = id, By = finalizedBy, At = finalizedAt, Finalized = PayrollPeriodStatus.Finalized, Draft = PayrollPeriodStatus.Draft },
            cancellationToken) == 1;
}

public class PayslipRepository : BaseRepository<Payslip, long>, IPayslipRepository
{
    public PayslipRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "payslips";

    private const string RowSelect = """
        SELECT s.*, u.`employee_code` AS EmployeeCode, u.`full_name` AS FullName, u.`email` AS Email, u.`avatar_url` AS AvatarUrl,
               pos.`title` AS PositionTitle, b.`name` AS BranchName, pr.`month` AS Month, pr.`year` AS Year, pr.`status` AS PeriodStatus
        FROM `payslips` s
        JOIN `payroll_periods` pr ON pr.`id` = s.`period_id`
        JOIN `users` u ON u.`id` = s.`user_id`
        LEFT JOIN `positions` pos ON pos.`id` = u.`position_id`
        LEFT JOIN `branches` b ON b.`id` = s.`branch_id`
        """;

    public async Task ReplaceForPeriodAsync(int periodId, IReadOnlyList<PayslipWithItems> payslips, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("DELETE FROM `payslips` WHERE `period_id` = @PeriodId", new { PeriodId = periodId }, cancellationToken); // dòng chi tiết xóa theo (ON DELETE CASCADE)

        foreach (var (payslip, items) in payslips)
        {
            payslip.PeriodId = periodId;
            var id = await AddAsync(payslip, cancellationToken);
            if (items.Count == 0) continue;

            await ExecuteAsync("""
                INSERT INTO `payslip_items` (`payslip_id`, `kind`, `name`, `quantity`, `unit_amount`, `amount`, `note`)
                VALUES (@PayslipId, @Kind, @Name, @Quantity, @UnitAmount, @Amount, @Note)
                """, items.Select(i => new { PayslipId = id, i.Kind, i.Name, i.Quantity, i.UnitAmount, i.Amount, i.Note }).ToList(), cancellationToken);
        }
    }

    public async Task<PaginatedResult<PayslipRow>> GetDetailsPagedAsync(PayslipQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "1 = 1" };
        var parameters = new DynamicParameters();
        void Add(bool when, string condition, string name, object? value)
        {
            if (!when) return;
            conditions.Add(condition);
            parameters.Add(name, value);
        }

        Add(query.PeriodId.HasValue, "s.`period_id` = @PeriodId", "PeriodId", query.PeriodId);
        Add(query.UserId.HasValue, "s.`user_id` = @UserId", "UserId", query.UserId);
        Add(query.BranchId.HasValue, "s.`branch_id` = @BranchId", "BranchId", query.BranchId);
        Add(query.Year.HasValue, "pr.`year` = @Year", "Year", query.Year);
        Add(!string.IsNullOrWhiteSpace(query.Status), "s.`status` = @Status", "Status", query.Status);
        Add(query.FinalizedOnly, "pr.`status` = @Finalized", "Finalized", PayrollPeriodStatus.Finalized);
        AddSearchCondition(query.Search, new[] { "u.`full_name`", "u.`employee_code`" }, conditions, parameters);
        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.NormalizedPageSize);

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT COUNT(*) FROM `payslips` s JOIN `payroll_periods` pr ON pr.`id` = s.`period_id` JOIN `users` u ON u.`id` = s.`user_id` WHERE {where};
            {RowSelect} WHERE {where} ORDER BY pr.`year` DESC, pr.`month` DESC, b.`name`, u.`employee_code` LIMIT @Take OFFSET @Skip;
            """;
        var connection = await Session.GetConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(Command(sql, parameters, cancellationToken));
        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<PayslipRow>()).ToList();
        return PaginatedResult<PayslipRow>.Create(items, total, query.NormalizedPageIndex, query.NormalizedPageSize);
    }

    public Task<PayslipRow?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<PayslipRow>($"{RowSelect} WHERE s.`id` = @Id", new { Id = id }, cancellationToken);

    public Task<IReadOnlyList<PayslipRow>> GetRowsByPeriodAsync(int periodId, bool onlyUnsent, IReadOnlyCollection<long>? ids = null,
        CancellationToken cancellationToken = default) =>
        QueryAsync<PayslipRow>($"""
            {RowSelect} WHERE s.`period_id` = @PeriodId{(onlyUnsent ? " AND s.`status` <> 'sent'" : string.Empty)}{(ids is { Count: > 0 } ? " AND s.`id` IN @Ids" : string.Empty)}
            ORDER BY u.`employee_code`
            """, new { PeriodId = periodId, Ids = ids }, cancellationToken);

    public Task<IReadOnlyList<PayslipItem>> GetItemsAsync(long payslipId, CancellationToken cancellationToken = default) =>
        QueryAsync<PayslipItem>("SELECT * FROM `payslip_items` WHERE `payslip_id` = @Id ORDER BY `id`", new { Id = payslipId }, cancellationToken);

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<PayslipItem>>> GetItemsByPayslipsAsync(IReadOnlyCollection<long> payslipIds,
        CancellationToken cancellationToken = default)
    {
        if (payslipIds.Count == 0) return new Dictionary<long, IReadOnlyList<PayslipItem>>();
        var items = await QueryAsync<PayslipItem>("SELECT * FROM `payslip_items` WHERE `payslip_id` IN @Ids ORDER BY `id`", new { Ids = payslipIds }, cancellationToken);
        return items.GroupBy(i => i.PayslipId).ToDictionary(g => g.Key, g => (IReadOnlyList<PayslipItem>)g.ToList());
    }

    public async Task<int> MarkSentAsync(IReadOnlyCollection<long> ids, DateTime sentAt, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return 0;
        return await ExecuteAsync("UPDATE `payslips` SET `status` = @Sent, `sent_at` = @At WHERE `id` IN @Ids",
            new { Ids = ids, At = sentAt, Sent = PayslipStatus.Sent }, cancellationToken);
    }

    public async Task<PayslipTotals> GetTotalsAsync(int periodId, CancellationToken cancellationToken = default)
    {
        var row = await QuerySingleOrDefaultAsync<(long Count, decimal Basic, decimal Allowance, decimal Overtime, decimal Penalty, decimal Net)>("""
            SELECT COUNT(*) AS Count, COALESCE(SUM(`basic_pay`), 0) AS Basic, COALESCE(SUM(`allowance_total`), 0) AS Allowance,
                   COALESCE(SUM(`overtime_pay`), 0) AS Overtime, COALESCE(SUM(`penalty_total`), 0) AS Penalty, COALESCE(SUM(`net_pay`), 0) AS Net
            FROM `payslips` WHERE `period_id` = @PeriodId
            """, new { PeriodId = periodId }, cancellationToken);
        return new PayslipTotals((int)row.Count, row.Basic, row.Allowance, row.Overtime, row.Penalty, row.Net);
    }
}
