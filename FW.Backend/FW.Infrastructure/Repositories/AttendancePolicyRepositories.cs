using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class ViolationRuleRepository : BaseRepository<ViolationRule>, IViolationRuleRepository
{
    public ViolationRuleRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "violation_rules";
    protected override string DefaultOrderBy => "`violation_type` ASC, `min_minutes` ASC";

    public async Task ReplaceAllAsync(IReadOnlyCollection<ViolationRule> rules, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("DELETE FROM `violation_rules`", null, cancellationToken);
        foreach (var rule in rules)
        {
            await AddAsync(rule, cancellationToken);
        }
    }
}

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly DbSession _session;

    public SystemSettingRepository(DbSession session)
    {
        _session = session;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);
        return await Dapper.SqlMapper.ExecuteScalarAsync<string?>(connection, new Dapper.CommandDefinition(
            "SELECT `setting_value` FROM `system_settings` WHERE `setting_key` = @Key",
            new { Key = key }, _session.Transaction, cancellationToken: cancellationToken));
    }

    public async Task SetValueAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO `system_settings` (`setting_key`, `setting_value`, `description`) VALUES (@Key, @Value, @Description)
            ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`),
                                    `description` = COALESCE(VALUES(`description`), `description`)
            """;
        var connection = await _session.GetConnectionAsync(cancellationToken);
        await Dapper.SqlMapper.ExecuteAsync(connection, new Dapper.CommandDefinition(
            sql, new { Key = key, Value = value, Description = description }, _session.Transaction, cancellationToken: cancellationToken));
    }
}
