using System.Net;
using FW.Infrastructure.Services.Caching;

namespace FW.UnitTests.Infrastructure;

/// <summary>Chuỗi kết nối Redis của Render Key Value / Upstash (dạng URL) và dạng StackExchange.Redis.</summary>
public class RedisConnectionParserTests
{
    [Fact]
    public void Parses_render_internal_url_without_credentials()
    {
        var options = RedisConnectionParser.Parse("redis://red-abc123:6379");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("red-abc123", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.False(options.Ssl);
        Assert.Null(options.Password);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void Parses_tls_url_with_default_user_and_password()
    {
        var options = RedisConnectionParser.Parse("rediss://default:p%40ss@cache.upstash.io:6380");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("cache.upstash.io", endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
        Assert.True(options.Ssl);
        Assert.Equal("cache.upstash.io", options.SslHost);
        Assert.Equal("p@ss", options.Password);
        Assert.Null(options.User);
    }

    [Fact]
    public void Parses_url_with_named_user()
    {
        var options = RedisConnectionParser.Parse("redis://app:secret@host:1234");

        Assert.Equal("app", options.User);
        Assert.Equal("secret", options.Password);
    }

    [Fact]
    public void Keeps_stackexchange_format()
    {
        var options = RedisConnectionParser.Parse("localhost:6379,password=abc");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("localhost", endpoint.Host);
        Assert.Equal("abc", options.Password);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void Rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => RedisConnectionParser.Parse("  "));
    }
}
