using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

public sealed record ChatRequest(long? ConversationId, string Message);

public sealed record DraftView(
    Guid Id,
    string Kind,
    string Title,
    IReadOnlyList<DraftLine> Lines,
    IReadOnlyList<string> Warnings,
    bool RequiresAttachment,
    string Status,
    DateTime ExpiresAt)
{
    public static DraftView From(AssistantDraft d) => new(
        d.Id, d.Card.Kind, d.Card.Title, d.Card.Lines, d.Card.Warnings, d.Card.RequiresAttachment,
        d.Status.ToString().ToLowerInvariant(), d.ExpiresAt);
}

public sealed record ChatReply(
    long ConversationId,
    string Reply,
    IReadOnlyList<DraftView> Drafts,
    IReadOnlyList<string> ToolsUsed);

/// <summary>Sự kiện khi trả lời theo luồng (SSE): giao diện hiển thị dần rồi thay bằng nội dung chính thức ở <see cref="DoneEvent"/>.</summary>
public abstract record AssistantEvent;

/// <summary>Mã hội thoại (gửi đầu tiên, để giao diện nhớ khi đây là câu hỏi mở đầu).</summary>
public sealed record ConversationEvent(long ConversationId) : AssistantEvent;

/// <summary>Trợ lý bắt đầu gọi một công cụ (giao diện hiện "đang tra lịch...").</summary>
public sealed record ToolStartEvent(string Name) : AssistantEvent;

public sealed record TextDeltaEvent(string Text) : AssistantEvent;

/// <summary>Lượt vừa rồi hóa ra là lượt gọi công cụ — xóa phần chữ xem trước, câu trả lời thật còn ở phía sau.</summary>
public sealed record ResetEvent : AssistantEvent;

public sealed record DoneEvent(ChatReply Reply) : AssistantEvent;

public sealed record MessageView(long Id, string Role, string Content, IReadOnlyList<DraftView> Drafts, DateTime CreatedAt);

/// <summary>
/// Trợ lý nhân sự: vòng lặp "mô hình → gọi công cụ → trả kết quả → mô hình" (Function Calling).
/// Mọi công cụ chạy với danh tính của người hỏi (lấy từ JWT); mô hình chỉ tạo BẢN NHÁP đơn, gửi đơn là việc của người dùng.
/// </summary>
public sealed class AssistantService
{
    private readonly ILlmChatClient _llm;
    private readonly IReadOnlyList<IAssistantTool> _tools;
    private readonly IAssistantStore _store;
    private readonly IDraftStore _drafts;
    private readonly IHrDataService _hr;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        ILlmChatClient llm,
        IEnumerable<IAssistantTool> tools,
        IAssistantStore store,
        IDraftStore drafts,
        IHrDataService hr,
        ICurrentUserService user,
        IDateTimeProvider clock,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        _llm = llm;
        _tools = tools.ToList();
        _store = store;
        _drafts = drafts;
        _hr = hr;
        _user = user;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Hỏi đáp trọn gói (không streaming).</summary>
    public Task<ChatReply> ChatAsync(ChatRequest request, CancellationToken ct = default) => RunAsync(request, null, ct);

    /// <summary>
    /// Hỏi đáp có streaming: phát <see cref="ConversationEvent"/>, các <see cref="TextDeltaEvent"/> / <see cref="ToolStartEvent"/>, rồi <see cref="DoneEvent"/>.
    /// Lỗi nghiệp vụ (hết hạn mức, không tìm thấy hội thoại...) được ném ra ngay ở lần đọc đầu tiên, trước khi có sự kiện nào.
    /// </summary>
    public async IAsyncEnumerable<AssistantEvent> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<AssistantEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        async Task WorkAsync()
        {
            try
            {
                await RunAsync(request, e =>
                {
                    channel.Writer.TryWrite(e);
                    return ValueTask.CompletedTask;
                }, ct);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }

        var worker = WorkAsync();
        await foreach (var e in channel.Reader.ReadAllAsync(ct))
            yield return e;
        await worker;
    }

    private async Task<ChatReply> RunAsync(ChatRequest request, Func<AssistantEvent, ValueTask>? emit, CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new UnauthorizedException("Chưa đăng nhập.");

        var text = request.Message?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new BadRequestException("Tin nhắn không được để trống.");
        if (text.Length > _options.MaxMessageLength)
            throw new BadRequestException($"Tin nhắn quá dài (tối đa {_options.MaxMessageLength} ký tự).");

        var today = _clock.Today;
        var used = await _store.CountUserMessagesOnAsync(userId, today, ct);
        if (used >= _options.MaxMessagesPerUserPerDay)
            throw new TooManyRequestsException($"Bạn đã dùng hết {_options.MaxMessagesPerUserPerDay} câu hỏi hôm nay. Vui lòng quay lại vào ngày mai.");

        AssistantConversation conversation;
        if (request.ConversationId is { } cid)
        {
            conversation = await _store.GetConversationAsync(cid, userId, ct)
                           ?? throw new NotFoundException("Không tìm thấy hội thoại.");
        }
        else
        {
            conversation = await _store.CreateConversationAsync(userId, Shorten(text, 60), _clock.Now, ct);
        }

        if (emit is not null) await emit(new ConversationEvent(conversation.Id));

        var history = await _store.GetRecentMessagesAsync(conversation.Id, _options.HistoryMessages, ct);

        var roles = _user.Roles;
        var allowedTools = _tools
            .Where(t => t.AllowedRoles is null || t.AllowedRoles.Any(r => roles.Contains(r)))
            .ToList();
        var toolDefs = allowedTools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.ParametersSchema)).ToList();

        var leaveTypes = await _hr.GetLeaveTypesAsync(ct);
        var messages = new List<LlmMessage> { LlmMessage.System(SystemPromptBuilder.Build(today, roles, leaveTypes)) };
        foreach (var h in history)
            messages.Add(h.Role == "assistant" ? LlmMessage.Assistant(h.Content) : LlmMessage.User(h.Content));
        messages.Add(LlmMessage.User(text));

        var recentUserText = string.Join("\n", history.Where(h => h.Role == "user").TakeLast(2).Select(h => h.Content).Append(text));
        var context = new ToolContext(userId, _user.BranchId, roles, conversation.Id, today, recentUserText);
        var drafts = new List<AssistantDraft>();
        var trace = new JsonArray();
        var toolsUsed = new List<string>();
        string? reply = null;
        var nudged = false;

        for (var i = 0; i < _options.MaxToolIterations; i++)
        {
            LlmResponse response;
            try
            {
                response = emit is null
                    ? await _llm.ChatAsync(messages, toolDefs, ct)
                    : await StreamTurnAsync(messages, toolDefs, emit, ct);
            }
            catch (LlmUnavailableException ex)
            {
                _logger.LogError(ex, "LLM không khả dụng");
                throw new ExternalServiceException("Trợ lý AI đang tạm thời không khả dụng, vui lòng thử lại sau ít phút.");
            }

            if (response.ToolCalls.Count == 0)
            {
                // Mô hình nhỏ hay "nói đã tạo bản nháp" mà không gọi công cụ nào → người dùng bấm xác nhận cũng không có gì.
                // Nhắc nó gọi đúng công cụ (một lần); nếu vẫn bịa thì thay bằng câu trả lời an toàn.
                if (drafts.Count == 0 && !toolsUsed.Any(n => n.StartsWith("draft_", StringComparison.Ordinal))
                    && ClaimsDraftOrSubmission(response.Content))
                {
                    if (!nudged)
                    {
                        nudged = true;
                        _logger.LogWarning("Mô hình báo đã tạo bản nháp nhưng chưa gọi công cụ nào; nhắc gọi lại.");
                        if (emit is not null) await emit(new ResetEvent());
                        messages.Add(LlmMessage.Assistant(response.Content));
                        messages.Add(LlmMessage.User(
                            "(Hệ thống) Bạn chưa gọi công cụ nào nên CHƯA có bản nháp nào cả. " +
                            "Hãy gọi đúng công cụ draft_* với thông tin người dùng vừa nói; nếu còn thiếu thông tin thì hỏi lại người dùng. " +
                            "Không được nói đã tạo hay đã gửi đơn khi chưa có kết quả từ công cụ."));
                        continue;
                    }

                    if (emit is not null) await emit(new ResetEvent());
                    reply = "Xin lỗi, mình chưa tạo được bản nháp. Bạn nói rõ giúp mình loại đơn, ngày, giờ (nếu có) và lý do nhé.";
                    break;
                }

                reply = response.Content;
                break;
            }

            messages.Add(LlmMessage.Assistant(response.Content, response.ToolCalls));
            foreach (var call in response.ToolCalls)
            {
                if (emit is not null) await emit(new ToolStartEvent(call.Name));
                var (json, draft) = await ExecuteToolAsync(allowedTools, context, call, ct);
                if (draft is not null) drafts.Add(draft);
                toolsUsed.Add(call.Name);
                trace.Add(new JsonObject { ["tool"] = call.Name, ["args"] = call.Arguments.DeepClone(), ["draft"] = draft?.Id.ToString() });
                messages.Add(LlmMessage.Tool(call.Name, json));
            }
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            // Hết vòng gọi công cụ mà mô hình chưa trả lời, hoặc trả rỗng
            reply = drafts.Count > 0
                ? "Mình đã soạn bản nháp bên dưới, bạn xem lại và bấm \"Xác nhận\" để gửi nhé."
                : "Xin lỗi, mình chưa xử lý được yêu cầu này. Bạn thử nói rõ hơn (ngày, ca, lý do) giúp mình nhé.";
        }

        var now = _clock.Now;
        await _store.AddMessageAsync(conversation.Id, "user", text, null, Array.Empty<Guid>(), now, ct);
        await _store.AddMessageAsync(conversation.Id, "assistant", reply, trace.ToJsonString(), drafts.Select(d => d.Id).ToList(), now, ct);

        var chat = new ChatReply(conversation.Id, reply, drafts.Select(DraftView.From).ToList(), toolsUsed);
        if (emit is not null) await emit(new DoneEvent(chat));
        return chat;
    }

    /// <summary>Câu trả lời khẳng định đã tạo bản nháp / đã gửi đơn (chỉ đáng tin khi có bản nháp thật).</summary>
    public static bool ClaimsDraftOrSubmission(string? content)
    {
        var t = RelativeDateResolver.Normalize(content);
        if (t.Length == 0) return false;
        return t.Contains("ban nhap") || t.Contains("nut xac nhan") || t.Contains("bam xac nhan")
               || t.Contains("da gui don") || t.Contains("da tao don") || t.Contains("da dang ky");
    }

    /// <summary>Một lượt gọi mô hình theo luồng: phát từng đoạn chữ ra ngoài, trả kết quả hoàn chỉnh của lượt.</summary>
    private async Task<LlmResponse> StreamTurnAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, Func<AssistantEvent, ValueTask> emit, CancellationToken ct)
    {
        LlmResponse? completed = null;
        var streamedText = false;
        await foreach (var ev in _llm.ChatStreamAsync(messages, tools, ct))
        {
            switch (ev)
            {
                case LlmTextDelta delta:
                    streamedText = true;
                    await emit(new TextDeltaEvent(delta.Text));
                    break;
                case LlmCompleted done:
                    completed = done.Response;
                    break;
            }
        }

        if (completed is null) throw new LlmUnavailableException("Mô hình kết thúc mà không trả kết quả.");
        // Lượt gọi công cụ mà đã lỡ phát chữ ra: xóa bản xem trước để câu trả lời thật (sau khi có số liệu) thay thế
        if (streamedText && completed.ToolCalls.Count > 0) await emit(new ResetEvent());
        return completed;
    }

    public Task<IReadOnlyList<AssistantConversation>> ListConversationsAsync(int limit = 30, CancellationToken ct = default)
    {
        var userId = _user.UserId ?? throw new UnauthorizedException("Chưa đăng nhập.");
        return _store.ListConversationsAsync(userId, Math.Clamp(limit, 1, 100), ct);
    }

    public async Task<IReadOnlyList<MessageView>> GetMessagesAsync(long conversationId, CancellationToken ct = default)
    {
        var userId = _user.UserId ?? throw new UnauthorizedException("Chưa đăng nhập.");
        _ = await _store.GetConversationAsync(conversationId, userId, ct)
            ?? throw new NotFoundException("Không tìm thấy hội thoại.");

        var messages = await _store.GetRecentMessagesAsync(conversationId, 200, ct);
        var result = new List<MessageView>(messages.Count);
        foreach (var m in messages)
        {
            var views = new List<DraftView>();
            foreach (var id in m.DraftIds)
            {
                var d = await _drafts.GetAsync(id, ct);
                if (d is not null && d.UserId == userId) views.Add(DraftView.From(d));
            }
            result.Add(new MessageView(m.Id, m.Role, m.Content, views, m.CreatedAt));
        }
        return result;
    }

    private async Task<(string Json, AssistantDraft? Draft)> ExecuteToolAsync(
        IReadOnlyList<IAssistantTool> allowed, ToolContext context, LlmToolCall call, CancellationToken ct)
    {
        var tool = allowed.FirstOrDefault(t => t.Name == call.Name);
        if (tool is null)
            return (Error($"Công cụ '{call.Name}' không tồn tại hoặc bạn không có quyền dùng."), null);

        try
        {
            var result = await tool.ExecuteAsync(context, call.Arguments, ct);
            return (Serialize(result.Data), result.Draft);
        }
        catch (ToolArgumentException ex)
        {
            return (Error(ex.Message), null);
        }
        catch (AppException ex)
        {
            return (Error(ex.Message), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Công cụ {Tool} lỗi", call.Name);
            return (Error("Công cụ gặp lỗi tạm thời, hãy báo người dùng thử lại sau."), null);
        }
    }

    private string Serialize(JsonNode data)
    {
        var json = data.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return json.Length <= _options.MaxToolResultChars
            ? json
            : json[.._options.MaxToolResultChars] + "…(đã cắt bớt)";
    }

    private static string Error(string message) =>
        new JsonObject { ["error"] = message }.ToJsonString(
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
