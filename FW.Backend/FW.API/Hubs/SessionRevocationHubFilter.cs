using FW.Application.Common.Security;
using FW.Application.Features.Auth;
using Microsoft.AspNetCore.SignalR;

namespace FW.API.Hubs;

/// <summary>
/// Áp dụng cho mọi Hub: đăng ký kết nối vào <see cref="ILiveSessionRegistry"/> để ngắt ngay khi phiên đăng nhập bị thu hồi
/// (đổi mật khẩu, khóa/xóa tài khoản, đổi vai trò hoặc chi nhánh).
/// </summary>
public class SessionRevocationHubFilter : IHubFilter
{
    private readonly ILiveSessionRegistry _liveSessions;

    public SessionRevocationHubFilter(ILiveSessionRegistry liveSessions) => _liveSessions = liveSessions;

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var connection = context.Context;
        var stamp = connection.User?.FindFirst(AppClaimTypes.SessionStamp)?.Value;
        if (!int.TryParse(connection.User?.FindFirst(AppClaimTypes.UserId)?.Value, out var userId) || stamp == null)
        {
            connection.Abort();
            return;
        }

        _liveSessions.Add(userId, connection.ConnectionId, stamp, connection.Abort);
        try
        {
            // Kiểm tra lại SAU khi đã đăng ký: phiên có thể bị thu hồi giữa lúc xác thực token và lúc này
            var sessions = context.ServiceProvider.GetRequiredService<ISessionStampService>();
            if (!await sessions.IsCurrentAsync(userId, stamp, connection.ConnectionAborted))
            {
                _liveSessions.Remove(connection.ConnectionId);
                connection.Abort();
                return;
            }
            await next(context);
        }
        catch
        {
            // Kết nối lỗi ngay khi mở thì OnDisconnectedAsync không được gọi → tự gỡ khỏi sổ
            _liveSessions.Remove(connection.ConnectionId);
            throw;
        }
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        _liveSessions.Remove(context.Context.ConnectionId);
        return next(context, exception);
    }
}
