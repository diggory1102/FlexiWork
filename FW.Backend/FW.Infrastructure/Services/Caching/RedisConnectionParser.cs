using StackExchange.Redis;

namespace FW.Infrastructure.Services.Caching;

/// <summary>
/// Đọc chuỗi kết nối Redis ở cả hai dạng:
/// - Dạng của StackExchange.Redis: <c>host:6379,password=...,ssl=True</c>
/// - Dạng URL mà Render Key Value / Upstash cấp: <c>redis://[user]:[password]@host:port</c> hoặc <c>rediss://…</c> (TLS)
/// </summary>
public static class RedisConnectionParser
{
    public static ConfigurationOptions Parse(string connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new ArgumentException("Chuỗi kết nối Redis rỗng.", nameof(connection));
        }

        connection = connection.Trim();
        ConfigurationOptions options;

        if (connection.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            connection.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connection);
            options = new ConfigurationOptions
            {
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
            };
            options.EndPoints.Add(uri.Host, uri.IsDefaultPort || uri.Port <= 0 ? 6379 : uri.Port);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                var user = Uri.UnescapeDataString(parts[0]);
                var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;

                if (password is null)
                {
                    // redis://password@host
                    options.Password = user;
                }
                else
                {
                    options.Password = password;
                    // "default" là user mặc định của Redis 6+ — không cần khai báo
                    if (!string.IsNullOrEmpty(user) && user != "default") options.User = user;
                }
            }

            if (options.Ssl) options.SslHost = uri.Host;
        }
        else
        {
            options = ConfigurationOptions.Parse(connection);
        }

        // Không chặn khởi động nếu Redis tạm thời chưa sẵn sàng; tự kết nối lại sau
        options.AbortOnConnectFail = false;
        return options;
    }
}
