namespace FW.Infrastructure.Database;

/// <summary>
/// Cấu hình toàn cục cho Dapper.
/// </summary>
public static class DapperConfig
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;

        // Tự động ánh xạ cột snake_case (created_at) sang thuộc tính PascalCase (CreatedAt)
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        _configured = true;
    }
}
