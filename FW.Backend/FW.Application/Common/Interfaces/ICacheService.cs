namespace FW.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ bộ nhớ đệm phân tán (Redis): lưu OTP, cache dữ liệu tra cứu.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
