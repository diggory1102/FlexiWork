using FW.Infrastructure.Services.Llm;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FW.UnitTests.Assistant;

public class OllamaChatClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
        public string? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return await _respond(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static OllamaChatClient Client(StubHandler handler, OllamaOptions? options = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") },
            Options.Create(options ?? new OllamaOptions { Model = "qwen2.5:7b" }),
            NullLogger<OllamaChatClient>.Instance);

    private static readonly LlmToolDefinition Tool = new(
        "get_my_leave_balance", "Xem quỹ phép",
        new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() });

    [Fact]
    public async Task Sends_model_messages_and_tools_in_ollama_format()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json("{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}")));
        var call = new LlmToolCall("get_my_leave_balance", new JsonObject { ["year"] = 2026 });

        await Client(handler).ChatAsync(new[]
        {
            LlmMessage.System("sys"),
            LlmMessage.User("Em còn mấy ngày phép?"),
            LlmMessage.Assistant(string.Empty, new[] { call }),
            LlmMessage.Tool("get_my_leave_balance", "{\"remaining_days\":11}")
        }, new[] { Tool });

        Assert.Equal("http://ollama.test/api/chat", handler.LastUri!.ToString());
        var body = JsonNode.Parse(handler.LastBody!)!;
        Assert.Equal("qwen2.5:7b", (string)body["model"]!);
        Assert.False((bool)body["stream"]!);
        Assert.Equal(0.1, (double)body["options"]!["temperature"]!, 3);

        var messages = body["messages"]!.AsArray();
        Assert.Equal(new[] { "system", "user", "assistant", "tool" }, messages.Select(m => (string)m!["role"]!));
        // arguments gửi dạng OBJECT (Ollama không nhận chuỗi)
        Assert.Equal(2026, (int)messages[2]!["tool_calls"]![0]!["function"]!["arguments"]!["year"]!);
        Assert.Equal("get_my_leave_balance", (string)messages[3]!["tool_name"]!);

        var tool = body["tools"]![0]!;
        Assert.Equal("function", (string)tool["type"]!);
        Assert.Equal("get_my_leave_balance", (string)tool["function"]!["name"]!);
        Assert.Equal("object", (string)tool["function"]!["parameters"]!["type"]!);
    }

    [Fact]
    public async Task Omits_tools_when_none_and_sends_think_only_when_configured()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json("{\"message\":{\"content\":\"x\"}}")));

        await Client(handler).ChatAsync(new[] { LlmMessage.User("hi") }, Array.Empty<LlmToolDefinition>());
        var plain = JsonNode.Parse(handler.LastBody!)!.AsObject();
        Assert.False(plain.ContainsKey("tools"));
        Assert.False(plain.ContainsKey("think"));

        await Client(handler, new OllamaOptions { Think = false }).ChatAsync(new[] { LlmMessage.User("hi") }, Array.Empty<LlmToolDefinition>());
        Assert.False((bool)JsonNode.Parse(handler.LastBody!)!["think"]!);
    }

    [Fact]
    public async Task Parses_tool_calls_with_object_arguments()
    {
        const string json = """
            {"message":{"role":"assistant","content":"","tool_calls":[
              {"function":{"name":"draft_leave_request","arguments":{"leave_type_id":1,"weekday":"thursday","reason":"bận thi"}}}]}}
            """;
        var res = await Client(new StubHandler(_ => Task.FromResult(Json(json)))).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool });

        var call = Assert.Single(res.ToolCalls);
        Assert.Equal("draft_leave_request", call.Name);
        Assert.Equal("bận thi", (string)call.Arguments["reason"]!);
        Assert.Equal(1, (int)call.Arguments["leave_type_id"]!);
    }

    [Fact]
    public async Task Parses_tool_calls_whose_arguments_come_as_a_json_string()
    {
        const string json = """
            {"message":{"content":"","tool_calls":[{"function":{"name":"get_my_schedule","arguments":"{\"from_date\":\"2026-11-19\"}"}}]}}
            """;
        var res = await Client(new StubHandler(_ => Task.FromResult(Json(json)))).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool });
        Assert.Equal("2026-11-19", (string)res.ToolCalls[0].Arguments["from_date"]!);
    }

    [Fact]
    public async Task Recovers_tool_calls_printed_as_text_by_qwen()
    {
        const string json = """
            {"message":{"content":"<tool_call>\n{\"name\": \"get_my_leave_balance\", \"arguments\": {\"year\": 2026}}\n</tool_call>"}}
            """;
        var res = await Client(new StubHandler(_ => Task.FromResult(Json(json)))).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool });

        var call = Assert.Single(res.ToolCalls);
        Assert.Equal("get_my_leave_balance", call.Name);
        Assert.Equal(2026, (int)call.Arguments["year"]!);
        Assert.Equal(string.Empty, res.Content);
    }

    [Fact]
    public async Task Strips_think_blocks_from_the_answer()
    {
        const string json = "{\"message\":{\"content\":\"<think>hmm...</think>Bạn còn 11 ngày phép.\"}}";
        var res = await Client(new StubHandler(_ => Task.FromResult(Json(json)))).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool });
        Assert.Equal("Bạn còn 11 ngày phép.", res.Content);
        Assert.Empty(res.ToolCalls);
    }

    // Một dòng NDJSON của Ollama: {"message":{"role":"assistant","content":...,"tool_calls":[...]},"done":...}
    private static string Line(string content, bool done = false, JsonArray? toolCalls = null)
    {
        var message = new JsonObject { ["role"] = "assistant", ["content"] = content };
        if (toolCalls is not null) message["tool_calls"] = toolCalls;
        return new JsonObject { ["message"] = message, ["done"] = done }.ToJsonString();
    }

    private static HttpResponseMessage Ndjson(params string[] lines) =>
        new(HttpStatusCode.OK) { Content = new StringContent(string.Join("\n", lines) + "\n", Encoding.UTF8, "application/x-ndjson") };

    private static async Task<List<LlmStreamEvent>> StreamAsync(OllamaChatClient client)
    {
        var list = new List<LlmStreamEvent>();
        await foreach (var e in client.ChatStreamAsync(new[] { LlmMessage.User("x") }, new[] { Tool })) list.Add(e);
        return list;
    }

    [Fact]
    public async Task Streaming_requests_stream_true_and_yields_each_piece_then_the_full_answer()
    {
        var handler = new StubHandler(_ => Task.FromResult(Ndjson(Line("Bạn "), Line("còn 11 ngày"), Line(string.Empty, done: true))));

        var events = await StreamAsync(Client(handler));

        Assert.True((bool)JsonNode.Parse(handler.LastBody!)!["stream"]!);
        Assert.Equal(new[] { "Bạn ", "còn 11 ngày" }, events.OfType<LlmTextDelta>().Select(e => e.Text));
        var done = Assert.IsType<LlmCompleted>(events[^1]);
        Assert.Equal("Bạn còn 11 ngày", done.Response.Content);
        Assert.Empty(done.Response.ToolCalls);
    }

    [Fact]
    public async Task Streaming_collects_tool_calls_and_hides_think_blocks()
    {
        var calls = new JsonArray(new JsonObject
        {
            ["function"] = new JsonObject { ["name"] = "get_my_leave_balance", ["arguments"] = new JsonObject { ["year"] = 2026 } }
        });
        var handler = new StubHandler(_ => Task.FromResult(Ndjson(
            Line("<think>để xem"), Line("</think>"), Line(string.Empty, toolCalls: calls), Line(string.Empty, done: true))));

        var events = await StreamAsync(Client(handler));

        Assert.Empty(events.OfType<LlmTextDelta>());
        var done = Assert.IsType<LlmCompleted>(events[^1]);
        var call = Assert.Single(done.Response.ToolCalls);
        Assert.Equal("get_my_leave_balance", call.Name);
        Assert.Equal(2026, (int)call.Arguments["year"]!);
        Assert.Equal(string.Empty, done.Response.Content);
    }

    [Fact]
    public async Task Streaming_recovers_a_tool_call_printed_as_text()
    {
        var handler = new StubHandler(_ => Task.FromResult(Ndjson(
            Line("<tool_call>{\"name\":\"get_my_leave_balance\","), Line("\"arguments\":{}}</tool_call>", done: true))));

        var events = await StreamAsync(Client(handler));

        Assert.Empty(events.OfType<LlmTextDelta>());
        Assert.Equal("get_my_leave_balance", Assert.Single(((LlmCompleted)events[^1]).Response.ToolCalls).Name);
    }

    [Fact]
    public async Task Streaming_http_error_becomes_llm_unavailable()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json("{\"error\":\"model not found\"}", HttpStatusCode.NotFound)));
        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => StreamAsync(Client(handler)));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Streaming_error_line_becomes_llm_unavailable()
    {
        var handler = new StubHandler(_ => Task.FromResult(Ndjson("{\"error\":\"out of memory\"}")));
        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => StreamAsync(Client(handler)));
        Assert.Contains("out of memory", ex.Message);
    }

    [Fact]
    public async Task Http_error_becomes_llm_unavailable_with_the_server_message()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json("{\"error\":\"model 'qwen2.5:7b' not found\"}", HttpStatusCode.NotFound)));
        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => Client(handler).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool }));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Connection_failure_becomes_llm_unavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("refused"));
        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => Client(handler).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool }));
        Assert.Contains("ollama.test", ex.Message.Replace("http://", string.Empty).Replace("localhost:11434", "ollama.test"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Garbage_response_becomes_llm_unavailable()
    {
        await Assert.ThrowsAsync<LlmUnavailableException>(() =>
            Client(new StubHandler(_ => Task.FromResult(Json("not json")))).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool }));
        await Assert.ThrowsAsync<LlmUnavailableException>(() =>
            Client(new StubHandler(_ => Task.FromResult(Json("{\"foo\":1}")))).ChatAsync(new[] { LlmMessage.User("x") }, new[] { Tool }));
    }
}
