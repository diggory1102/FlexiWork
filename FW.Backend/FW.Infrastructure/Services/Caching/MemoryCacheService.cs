using System.Text.Json;
using FW.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FW.Infrastructure.Services.Caching;

/// <summary>
/// Bộ nhớ đệm trong tiến trình — dùng thay Redis khi phát triển cục bộ chưa chạy Redis
/// (Redis:ConnectionString để trống). Không dùng cho môi trường nhiều instance.
/// Giá trị được tuần tự hóa JSON giống RedisCacheService để hành vi hai bên như nhau.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = _cache.TryGetValue(key, out string? json) && json != null
            ? JsonSerializer.Deserialize<T>(json)
            : default;
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        if (expiry.HasValue) _cache.Set(key, json, expiry.Value);
        else _cache.Set(key, json);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cache.TryGetValue(key, out _));
}
