using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;

namespace FW.Application.Features.Auth;

/// <summary>
/// Thu hồi phiên đăng nhập khi đổi mật khẩu. Mỗi JWT mang claim "pwd_stamp" = dấu phiên của người dùng
/// lúc phát hành; khi mật khẩu đổi, dấu phiên trong CSDL đổi theo nên mọi token cũ không còn khớp.
/// </summary>
public interface ISessionStampService
{
    /// <summary>Ghi nhận mật khẩu vừa đổi (chỉ gán thuộc tính — bên gọi tự lưu User).</summary>
    void MarkPasswordChanged(User user);

    /// <summary>Xóa dấu phiên trong cache sau khi đã lưu User, để lần kiểm tra kế tiếp đọc giá trị mới từ CSDL.</summary>
    Task InvalidateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Token còn hiệu lực khi người dùng còn tồn tại và dấu phiên trong token khớp dấu phiên hiện tại.</summary>
    Task<bool> IsCurrentAsync(int userId, long stamp, CancellationToken cancellationToken = default);
}

public class SessionStampService : ISessionStampService
{
    /// <summary>Kiểm tra token chạy ở mọi request nên dấu phiên được cache, tránh truy vấn CSDL liên tục.</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    private readonly IUserRepository _users;
    private readonly ICacheService _cache;
    private readonly IDateTimeProvider _clock;

    public SessionStampService(IUserRepository users, ICacheService cache, IDateTimeProvider clock)
    {
        _users = users;
        _cache = cache;
        _clock = clock;
    }

    /// <summary>Dấu phiên của người dùng: giây Unix của lần đổi mật khẩu gần nhất, 0 nếu chưa đổi.</summary>
    public static long StampOf(User user) => user.PasswordChangedAt is { } changedAt
        ? new DateTimeOffset(DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)).ToUnixTimeSeconds()
        : 0;

    public void MarkPasswordChanged(User user)
    {
        // Cột DATETIME chỉ lưu tới giây → làm tròn trước để giá trị đọc lại từ CSDL khớp với token mới
        var now = _clock.UtcNow;
        user.PasswordChangedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    public Task InvalidateAsync(int userId, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(CacheKey(userId), cancellationToken);

    public async Task<bool> IsCurrentAsync(int userId, long stamp, CancellationToken cancellationToken = default)
    {
        var current = await _cache.GetAsync<long?>(CacheKey(userId), cancellationToken);
        if (current == null)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null) return false; // tài khoản đã bị xóa
            current = StampOf(user);
            await _cache.SetAsync<long?>(CacheKey(userId), current, CacheLifetime, cancellationToken);
        }
        return current == stamp;
    }

    private static string CacheKey(int userId) => $"auth:session_stamp:{userId}";
}
