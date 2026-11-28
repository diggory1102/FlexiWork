using System.Security.Cryptography;
using System.Text;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Auth;

/// <summary>
/// Thu hồi phiên đăng nhập. Mỗi JWT mang claim "session_stamp" = dấu phiên lúc phát hành, là mã băm của lần đổi mật khẩu gần nhất,
/// trạng thái tài khoản, chi nhánh và vai trò. Một trong số đó đổi thì mọi token cũ không còn khớp; tài khoản bị khóa, đã nghỉ
/// hoặc đã xóa thì không token nào hợp lệ.
/// </summary>
public interface ISessionStampService
{
    /// <summary>Ghi nhận mật khẩu vừa đổi (chỉ gán thuộc tính — bên gọi tự lưu User).</summary>
    void MarkPasswordChanged(User user);

    /// <summary>
    /// Gọi sau khi đã lưu thay đổi của người dùng: xóa dấu phiên trong cache để lần kiểm tra kế tiếp đọc lại từ CSDL,
    /// đồng thời ngắt các kết nối thời gian thực đang dùng phiên không còn hiệu lực.
    /// </summary>
    Task InvalidateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Token còn hiệu lực khi tài khoản đang hoạt động và dấu phiên trong token khớp dấu phiên hiện tại.</summary>
    Task<bool> IsCurrentAsync(int userId, string? stamp, CancellationToken cancellationToken = default);
}

public class SessionStampService : ISessionStampService
{
    /// <summary>
    /// Kiểm tra token chạy ở mọi request nên dấu phiên được cache. Mọi thay đổi qua ứng dụng đều xóa cache ngay;
    /// thời hạn này chỉ giới hạn độ trễ khi dữ liệu bị sửa trực tiếp trong CSDL.
    /// </summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly IUserRepository _users;
    private readonly ICacheService _cache;
    private readonly IDateTimeProvider _clock;
    private readonly ILiveSessionRegistry _liveSessions;

    public SessionStampService(IUserRepository users, ICacheService cache, IDateTimeProvider clock, ILiveSessionRegistry liveSessions)
    {
        _users = users;
        _cache = cache;
        _clock = clock;
        _liveSessions = liveSessions;
    }

    /// <summary>Dấu phiên ứng với trạng thái hiện tại của người dùng. Thứ tự vai trò không ảnh hưởng kết quả.</summary>
    public static string StampOf(User user, IEnumerable<string> roles)
    {
        var passwordChangedAt = user.PasswordChangedAt is { } changedAt
            ? new DateTimeOffset(DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)).ToUnixTimeSeconds()
            : 0;
        var state = $"{passwordChangedAt}|{user.Status}|{user.BranchId}|{string.Join(',', roles.Order(StringComparer.Ordinal))}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)), 0, 16);
    }

    public void MarkPasswordChanged(User user)
    {
        // Cột DATETIME chỉ lưu tới giây → làm tròn trước để giá trị đọc lại từ CSDL khớp với token mới
        var now = _clock.UtcNow;
        user.PasswordChangedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    public async Task InvalidateAsync(int userId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKey(userId), cancellationToken);

        // Kết nối SignalR chỉ kiểm tra token lúc mở nên phải chủ động ngắt; sửa thông tin không ảnh hưởng phiên thì giữ nguyên
        if (_liveSessions.HasConnections(userId))
        {
            _liveSessions.AbortOutdated(userId, await GetCurrentStampAsync(userId, cancellationToken));
        }
    }

    public async Task<bool> IsCurrentAsync(int userId, string? stamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(stamp)) return false;
        return await GetCurrentStampAsync(userId, cancellationToken) == stamp;
    }

    /// <summary>Dấu phiên hiện tại; null nếu tài khoản không được phép dùng phiên nào (đã xóa, bị khóa, đã nghỉ).</summary>
    private async Task<string?> GetCurrentStampAsync(int userId, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync<string>(CacheKey(userId), cancellationToken);
        if (cached != null) return cached;

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user == null || user.Status != UserStatus.Active) return null;

        var stamp = StampOf(user, await _users.GetRoleNamesAsync(userId, cancellationToken));
        await _cache.SetAsync(CacheKey(userId), stamp, CacheLifetime, cancellationToken);
        return stamp;
    }

    // Tên khóa khác bản cũ (lưu số) để không đọc nhầm dữ liệu còn sót trong Redis sau khi triển khai
    private static string CacheKey(int userId) => $"auth:session_state:{userId}";
}
