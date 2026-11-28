using System.Collections.Concurrent;

namespace FW.Application.Features.Auth;

/// <summary>
/// Sổ các kết nối thời gian thực đang mở, để ngắt khi phiên đăng nhập của chúng bị thu hồi.
/// Lưu trong bộ nhớ của tiến trình nên chỉ đúng khi API chạy một instance; chạy nhiều instance thì cần thêm kênh phát tán
/// (cùng lúc với SignalR backplane).
/// </summary>
public interface ILiveSessionRegistry
{
    /// <summary>Ghi nhận một kết nối, dấu phiên của token dùng để mở nó và cách ngắt nó.</summary>
    void Add(int userId, string connectionId, string stamp, Action abort);

    void Remove(string connectionId);

    bool HasConnections(int userId);

    /// <summary>
    /// Ngắt mọi kết nối của người dùng có dấu phiên khác <paramref name="currentStamp"/> (null = ngắt tất cả).
    /// Trả về số kết nối đã ngắt.
    /// </summary>
    int AbortOutdated(int userId, string? currentStamp);
}

public class LiveSessionRegistry : ILiveSessionRegistry
{
    private sealed record Connection(int UserId, string Stamp, Action Abort);

    private readonly ConcurrentDictionary<string, Connection> _connections = new();

    public void Add(int userId, string connectionId, string stamp, Action abort) =>
        _connections[connectionId] = new Connection(userId, stamp, abort);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public bool HasConnections(int userId) => _connections.Any(c => c.Value.UserId == userId);

    public int AbortOutdated(int userId, string? currentStamp)
    {
        var aborted = 0;
        foreach (var (connectionId, connection) in _connections)
        {
            if (connection.UserId != userId || connection.Stamp == currentStamp) continue;
            if (!_connections.TryRemove(connectionId, out _)) continue;
            connection.Abort();
            aborted++;
        }
        return aborted;
    }
}
