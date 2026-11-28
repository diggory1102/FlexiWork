using FW.Application.Features.Auth;

namespace FW.UnitTests.Auth;

public class LiveSessionRegistryTests
{
    private readonly LiveSessionRegistry _registry = new();
    private readonly List<string> _aborted = new();

    private void Connect(int userId, string connectionId, string stamp) =>
        _registry.Add(userId, connectionId, stamp, () => _aborted.Add(connectionId));

    [Fact]
    public void Aborts_only_connections_with_an_outdated_stamp()
    {
        Connect(1, "old", "a");
        Connect(1, "new", "b");

        Assert.Equal(1, _registry.AbortOutdated(1, "b"));

        Assert.Equal(new[] { "old" }, _aborted);
        Assert.True(_registry.HasConnections(1));
    }

    [Fact]
    public void Null_stamp_aborts_every_connection_of_the_user_once()
    {
        Connect(1, "web", "a");
        Connect(1, "mobile", "a");

        Assert.Equal(2, _registry.AbortOutdated(1, null));
        Assert.Equal(0, _registry.AbortOutdated(1, null));

        Assert.False(_registry.HasConnections(1));
        Assert.Equal(2, _aborted.Count);
    }

    [Fact]
    public void Removed_connection_is_forgotten()
    {
        Connect(1, "web", "a");

        _registry.Remove("web");
        _registry.Remove("web"); // gỡ lần nữa không lỗi

        Assert.False(_registry.HasConnections(1));
        Assert.Equal(0, _registry.AbortOutdated(1, null));
        Assert.Empty(_aborted);
    }
}
