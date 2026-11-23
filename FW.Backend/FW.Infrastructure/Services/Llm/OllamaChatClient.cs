using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FW.Application.Features.Assistant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Infrastructure.Services.Llm;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Mô hình Qwen hỗ trợ tool calling (qwen2.5:7b, qwen2.5:14b, qwen3:8b...).</summary>
    public string Model { get; set; } = "qwen2.5:7b";

    /// <summary>Thấp để công cụ được gọi ổn định và số liệu không bị "sáng tác".</summary>
    public double Temperature { get; set; } = 0.1;

    public int NumCtx { get; set; } = 8192;
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>Thời gian giữ mô hình trong RAM sau lần gọi cuối (định dạng Ollama: "10m", "1h").</summary>
    public string KeepAlive { get; set; } = "10m";

    /// <summary>Với họ qwen3 (có chế độ suy nghĩ) đặt false để tắt suy nghĩ cho nhanh; để null thì không gửi tham số.</summary>
    public bool? Think { get; set; }

    /// <summary>
    /// Khóa gửi kèm dạng <c>Authorization: Bearer ...</c>. Ollama không có xác thực; khi đặt sau reverse proxy
    /// (Caddy/nginx) kiểm tra khóa này thì khai báo ở đây. Bỏ trống nếu Ollama chạy nội bộ không qua proxy.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>Gọi Ollama <c>POST /api/chat</c> kèm <c>tools</c>, có cả chế độ không streaming và streaming (NDJSON).</summary>
public sealed partial class OllamaChatClient : ILlmChatClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaChatClient> _logger;

    public OllamaChatClient(HttpClient http, IOptions<OllamaOptions> options, ILogger<OllamaChatClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> ChatAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        var body = BuildRequest(messages, tools);

        // Thời gian chờ tổng do lớp này kiểm soát (HttpClient.Timeout để vô hạn vì còn dùng cho streaming)
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("api/chat", body, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new LlmUnavailableException($"Không kết nối được Ollama tại {_options.BaseUrl}: {ex.Message}", ex);
        }

        using (response)
        {
            string raw;
            try
            {
                raw = await response.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new LlmUnavailableException("Mất kết nối tới Ollama khi đang đọc câu trả lời.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama trả {Status}: {Body}", (int)response.StatusCode, Truncate(raw, 500));
                throw new LlmUnavailableException($"Ollama lỗi {(int)response.StatusCode}: {ExtractError(raw)}");
            }

            return ParseResponse(raw);
        }
    }

    public async IAsyncEnumerable<LlmStreamEvent> ChatStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat") { Content = JsonContent.Create(BuildRequest(messages, tools, stream: true)) };
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new LlmUnavailableException($"Không kết nối được Ollama tại {_options.BaseUrl}: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama trả {Status}: {Body}", (int)response.StatusCode, Truncate(raw, 500));
                throw new LlmUnavailableException($"Ollama lỗi {(int)response.StatusCode}: {ExtractError(raw)}");
            }

            var filter = new StreamTextFilter();
            var content = new System.Text.StringBuilder();
            var calls = new List<LlmToolCall>();
            var started = false;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(timeout.Token);
                }
                catch (Exception ex) when (ex is IOException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    throw new LlmUnavailableException("Mất kết nối tới Ollama khi đang trả lời.", ex);
                }
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonObject chunk;
                try { chunk = JsonNode.Parse(line) as JsonObject ?? throw new JsonException("không phải object"); }
                catch (JsonException ex) { throw new LlmUnavailableException("Ollama trả dòng dữ liệu không hợp lệ.", ex); }

                if (chunk["error"]?.GetValue<string>() is { Length: > 0 } error)
                    throw new LlmUnavailableException($"Ollama lỗi: {error}");

                if (chunk["message"] is JsonObject message)
                {
                    calls.AddRange(ReadToolCalls(message));
                    var delta = message["content"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(delta))
                    {
                        content.Append(delta);
                        var visible = filter.Push(delta);
                        if (!started) visible = visible.TrimStart();
                        if (visible.Length > 0)
                        {
                            started = true;
                            yield return new LlmTextDelta(visible);
                        }
                    }
                }

                if (chunk["done"]?.GetValue<bool>() == true) break;
            }

            var tail = filter.Flush();
            if (!started) tail = tail.TrimStart();
            if (tail.Length > 0) yield return new LlmTextDelta(tail);

            yield return new LlmCompleted(Finish(content.ToString(), calls));
        }
    }

    internal JsonObject BuildRequest(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, bool stream = false)
    {
        var msgs = new JsonArray();
        foreach (var m in messages)
        {
            var node = new JsonObject { ["role"] = m.Role, ["content"] = m.Content };
            if (m.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var c in m.ToolCalls)
                {
                    calls.Add(new JsonObject
                    {
                        ["function"] = new JsonObject
                        {
                            ["name"] = c.Name,
                            ["arguments"] = c.Arguments.DeepClone()
                        }
                    });
                }
                node["tool_calls"] = calls;
            }
            if (m.ToolName is not null) node["tool_name"] = m.ToolName;
            msgs.Add(node);
        }

        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["stream"] = stream,
            ["keep_alive"] = _options.KeepAlive,
            ["messages"] = msgs,
            ["options"] = new JsonObject
            {
                ["temperature"] = _options.Temperature,
                ["num_ctx"] = _options.NumCtx
            }
        };
        if (_options.Think is { } think) body["think"] = think;

        if (tools.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var t in tools)
            {
                arr.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = t.ParametersSchema.DeepClone()
                    }
                });
            }
            body["tools"] = arr;
        }

        return body;
    }

    internal static LlmResponse ParseResponse(string raw)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(raw); }
        catch (JsonException ex) { throw new LlmUnavailableException("Ollama trả dữ liệu không phải JSON.", ex); }

        var message = root?["message"] as JsonObject
            ?? throw new LlmUnavailableException("Phản hồi Ollama thiếu trường 'message'.");

        var content = message["content"]?.GetValue<string>() ?? string.Empty;
        return Finish(content, ReadToolCalls(message));
    }

    private static List<LlmToolCall> ReadToolCalls(JsonObject message)
    {
        var calls = new List<LlmToolCall>();
        if (message["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var tc in toolCalls)
            {
                var fn = tc?["function"] as JsonObject;
                var name = fn?["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;
                calls.Add(new LlmToolCall(name, ReadArguments(fn!["arguments"])));
            }
        }
        return calls;
    }

    /// <summary>Chuẩn hóa kết quả cuối: gom lệnh gọi công cụ bị in thành văn bản, bỏ đoạn suy nghĩ.</summary>
    internal static LlmResponse Finish(string content, List<LlmToolCall> calls)
    {
        // Qwen đôi khi in lệnh gọi công cụ thành văn bản <tool_call>{...}</tool_call> thay vì trường tool_calls.
        if (calls.Count == 0 && content.Contains("<tool_call>", StringComparison.Ordinal))
        {
            foreach (Match m in ToolCallTagRegex().Matches(content))
            {
                try
                {
                    if (JsonNode.Parse(m.Groups[1].Value) is JsonObject obj && obj["name"]?.GetValue<string>() is { Length: > 0 } n)
                        calls.Add(new LlmToolCall(n, ReadArguments(obj["arguments"])));
                }
                catch (JsonException) { /* bỏ qua đoạn hỏng */ }
            }
            content = ToolCallTagRegex().Replace(content, string.Empty);
        }

        content = ThinkTagRegex().Replace(content, string.Empty).Trim();
        return new LlmResponse(content, calls);
    }

    private static JsonObject ReadArguments(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                return (JsonObject)o.DeepClone();
            case JsonValue v when v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s):
                try { return JsonNode.Parse(s) as JsonObject ?? new JsonObject(); }
                catch (JsonException) { return new JsonObject(); }
            default:
                return new JsonObject();
        }
    }

    private static string ExtractError(string raw)
    {
        try { return JsonNode.Parse(raw)?["error"]?.GetValue<string>() ?? Truncate(raw, 200); }
        catch (JsonException) { return Truncate(raw, 200); }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"<tool_call>\s*(\{.*?\})\s*</tool_call>", RegexOptions.Singleline)]
    private static partial Regex ToolCallTagRegex();

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkTagRegex();
}

/// <summary>
/// Lọc luồng chữ trước khi đưa cho người dùng: bỏ đoạn &lt;think&gt;…&lt;/think&gt; và &lt;tool_call&gt;…&lt;/tool_call&gt;
/// (kể cả khi thẻ bị cắt đôi giữa hai đoạn dữ liệu). Chỉ là bản xem trước; kết quả chính thức do <c>Finish</c> tính lại từ toàn bộ nội dung.
/// </summary>
public sealed class StreamTextFilter
{
    private static readonly (string Open, string Close)[] Tags = { ("<think>", "</think>"), ("<tool_call>", "</tool_call>") };

    private string _buffer = string.Empty;
    private string? _closing;

    public string Push(string delta)
    {
        _buffer += delta;
        var output = new System.Text.StringBuilder();

        while (true)
        {
            if (_closing is not null)
            {
                var close = _buffer.IndexOf(_closing, StringComparison.Ordinal);
                if (close < 0)
                {
                    // Bỏ phần đang nằm trong thẻ, chỉ giữ phần đuôi có thể là đầu của thẻ đóng
                    _buffer = _buffer[^PrefixLength(_buffer, new[] { _closing })..];
                    return output.ToString();
                }
                _buffer = _buffer[(close + _closing.Length)..];
                _closing = null;
                continue;
            }

            var best = -1;
            var bestTag = 0;
            for (var i = 0; i < Tags.Length; i++)
            {
                var at = _buffer.IndexOf(Tags[i].Open, StringComparison.Ordinal);
                if (at >= 0 && (best < 0 || at < best)) (best, bestTag) = (at, i);
            }

            if (best >= 0)
            {
                output.Append(_buffer, 0, best);
                _buffer = _buffer[(best + Tags[bestTag].Open.Length)..];
                _closing = Tags[bestTag].Close;
                continue;
            }

            var hold = PrefixLength(_buffer, Tags.Select(t => t.Open).ToArray());
            output.Append(_buffer, 0, _buffer.Length - hold);
            _buffer = _buffer[^hold..];
            return output.ToString();
        }
    }

    /// <summary>Hết luồng: trả phần còn giữ lại (nếu không nằm trong thẻ dở dang).</summary>
    public string Flush()
    {
        var rest = _closing is null ? _buffer : string.Empty;
        _buffer = string.Empty;
        _closing = null;
        return rest;
    }

    /// <summary>Độ dài đoạn đuôi dài nhất của <paramref name="text"/> là đầu (thật sự, chưa đủ) của một trong các thẻ.</summary>
    private static int PrefixLength(string text, string[] tags)
    {
        var max = 0;
        foreach (var tag in tags)
        {
            for (var len = Math.Min(text.Length, tag.Length - 1); len > max; len--)
            {
                if (text.EndsWith(tag[..len], StringComparison.Ordinal))
                {
                    max = len;
                    break;
                }
            }
        }
        return max;
    }
}
