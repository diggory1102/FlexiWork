namespace FW.Infrastructure.Database;

/// <summary>
/// Cấu hình toàn cục cho Dapper. Gọi nhiều lần vẫn an toàn (chỉ chạy lần đầu).
/// </summary>
public static class DapperConfig
{
    private static readonly object Gate = new();
    private static bool _configured;

    public static void Configure()
    {
        lock (Gate)
        {
            if (_configured) return;

            // Tự động ánh xạ cột snake_case (created_at) sang thuộc tính PascalCase (CreatedAt)
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            // Vector khuôn mặt lưu ở cột JSON
            Dapper.SqlMapper.AddTypeHandler(new FloatArrayJsonTypeHandler());

            _configured = true;
        }
    }
}
