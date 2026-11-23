using System.Text.Json.Nodes;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

public sealed record LlmToolCall(string Name, JsonObject Arguments);

/// <summary>Một thông điệp gửi cho mô hình. Role: system | user | assistant | tool.</summary>
public sealed record LlmMessage(
    string Role,
    string Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolName = null)
{
    public static LlmMessage System(string content) => new("system", content);
    public static LlmMessage User(string content) => new("user", content);
    public static LlmMessage Assistant(string content, IReadOnlyList<LlmToolCall>? calls = null) => new("assistant", content, calls);
    public static LlmMessage Tool(string toolName, string content) => new("tool", content, null, toolName);
}

/// <summary>Khai báo một công cụ (function) cho mô hình — <see cref="ParametersSchema"/> là JSON Schema.</summary>
public sealed record LlmToolDefinition(string Name, string Description, JsonObject ParametersSchema);

public sealed record LlmResponse(string Content, IReadOnlyList<LlmToolCall> ToolCalls);

/// <summary>Sự kiện khi mô hình trả lời theo luồng: từng đoạn chữ, rồi kết quả hoàn chỉnh (cả lệnh gọi công cụ nếu có).</summary>
public abstract record LlmStreamEvent;

public sealed record LlmTextDelta(string Text) : LlmStreamEvent;

public sealed record LlmCompleted(LlmResponse Response) : LlmStreamEvent;

/// <summary>Mô hình không gọi được (Ollama tắt, sai tên model, quá thời gian...).</summary>
public sealed class LlmUnavailableException : Exception
{
    public LlmUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Cổng tới LLM có Function Calling. Cài đặt mặc định: <see cref="OllamaChatClient"/>.</summary>
public interface ILlmChatClient
{
    Task<LlmResponse> ChatAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Như <see cref="ChatAsync"/> nhưng trả lời theo luồng: phát từng đoạn chữ ngay khi mô hình sinh ra, kết thúc bằng
    /// đúng một <see cref="LlmCompleted"/>. Đoạn chữ chỉ là bản xem trước — nội dung chính thức là của <see cref="LlmCompleted"/>.
    /// </summary>
    IAsyncEnumerable<LlmStreamEvent> ChatStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken = default);
}
