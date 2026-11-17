using FW.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FW.API.Hubs;

/// <summary>
/// Kênh thông báo tức thì (/hubs/notifications). Mỗi người dùng tham gia nhóm riêng <c>User_{id}</c> khi kết nối nên mọi thiết bị
/// đang đăng nhập của họ (Web, điện thoại) đều nhận được. Máy chủ gọi <see cref="ReceiveNotificationMethod"/> trên client —
/// client không gọi được phương thức nào lên Hub.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>Tên phương thức client lắng nghe.</summary>
    public const string ReceiveNotificationMethod = "ReceiveNotification";

    public static string GroupFor(int userId) => $"User_{userId}";

    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirst(AppClaimTypes.UserId)?.Value, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
        }
        await base.OnConnectedAsync();
    }
}
