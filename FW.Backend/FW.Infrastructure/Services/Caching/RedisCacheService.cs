using System.Text.Json;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FW.Infrastructure.Services.Caching;

/// <summary>
/// Bộ nhớ đệm phân tán sử dụng Redis (StackExchange.Redis), giá trị tuần tự hóa dạng JSON.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;

    public RedisCacheService(IConnectionMultiplexer redis, IOptions<RedisSettings> settings)
    {
        _redis = redis;
        _prefix = settings.Value.InstanceName;
    }

    private IDatabase Db => _redis.GetDatabase();
    private string Key(string key) => _prefix + key;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await Db.StringGetAsync(Key(key));
        return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString());
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        return expiry.HasValue
            ? Db.StringSetAsync(Key(key), json, expiry.Value)
            : Db.StringSetAsync(Key(key), json);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => Db.KeyDeleteAsync(Key(key));

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Db.KeyExistsAsync(Key(key));
}
