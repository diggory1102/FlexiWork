using System.Text.Json;
using FW.API.Extensions;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

public class ChatBody
{
    /// <summary>Bỏ trống = bắt đầu hội thoại mới.</summary>
    public long? ConversationId { get; set; }

    public string? Message { get; set; }
}

/// <summary>
/// Trợ lý nhân sự AI (UC 10.1 – 10.2): hỏi đáp lịch ca / công / phép / đơn từ và soạn BẢN NHÁP đơn (nghỉ, đổi ca, OT, giải trình).
/// Trợ lý không tự gửi đơn — người dùng phải xác nhận bản nháp. Mô hình chạy bằng Ollama + Qwen.
/// </summary>
[Authorize]
[Route("api/assistant")]
public class AssistantController : ApiControllerBase
{
    private const long MaxConfirmBytes = 6 * 1024 * 1024;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly AssistantService _assistant;
    private readonly DraftService _drafts;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(AssistantService assistant, DraftService drafts, ICurrentUserService currentUser, ILogger<AssistantController> logger)
    {
        _assistant = assistant;
        _drafts = drafts;
        _currentUser = currentUser;
        _logger = logger;
    }

    private int UserId => _currentUser.UserId ?? throw new UnauthorizedException();

    /// <summary>Hỏi trợ lý, trả lời trọn gói một lần (không streaming): {conversationId?, message}.</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatBody body, CancellationToken cancellationToken) =>
        Success(await _assistant.ChatAsync(new ChatRequest(body.ConversationId, body.Message ?? string.Empty), cancellationToken));

    /// <summary>
    /// Hỏi trợ lý, trả lời theo luồng SSE (text/event-stream). Các sự kiện: <c>conversation</c> {conversationId}, <c>tool</c> {name},
    /// <c>delta</c> {text}, <c>reset</c>, <c>done</c> {conversationId, reply, drafts, toolsUsed}, <c>error</c> {message, status}.
    /// Nội dung chính thức là của <c>done</c>; <c>delta</c> chỉ để hiển thị dần.
    /// </summary>
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] ChatBody body, CancellationToken cancellationToken)
    {
        await using var events = _assistant
            .ChatStreamAsync(new ChatRequest(body.ConversationId, body.Message ?? string.Empty), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        // Đọc sự kiện đầu TRƯỚC khi mở luồng: lỗi nghiệp vụ (hết hạn mức, không thấy hội thoại...) vẫn trả JSON + mã HTTP chuẩn
        var hasEvent = await events.MoveNextAsync();

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no"; // proxy (nginx / Cloudflare) không được gom đệm
        await Response.Body.FlushAsync(cancellationToken);

        try
        {
            while (hasEvent)
            {
                await WriteEventAsync(events.Current, cancellationToken);

                // Mô hình nạp lạnh có thể im lặng cả phút; gửi dòng chú thích định kỳ để Cloudflare/proxy không cắt kết nối "rảnh"
                var next = events.MoveNextAsync().AsTask();
                while (!next.IsCompleted)
                {
                    var finished = await Task.WhenAny(next, Task.Delay(HeartbeatInterval, cancellationToken));
                    if (finished != next) await WriteRawAsync(": ping\n\n", cancellationToken);
                }
                hasEvent = await next;
            }
        }
        catch (OperationCanceledException)
        {
            // Người dùng đóng màn hình giữa chừng — không có gì để báo
        }
        catch (Exception ex)
        {
            var status = ex is AppException app ? (int)app.StatusCode : StatusCodes.Status500InternalServerError;
            if (ex is not AppException) _logger.LogError(ex, "Lỗi khi streaming trả lời của trợ lý");
            var message = ex is AppException ? ex.Message : "Trợ lý gặp lỗi, vui lòng thử lại.";
            await WriteAsync("error", new { message, status }, CancellationToken.None);
        }
    }

    private Task WriteEventAsync(AssistantEvent ev, CancellationToken ct) => ev switch
    {
        ConversationEvent c => WriteAsync("conversation", new { conversationId = c.ConversationId }, ct),
        ToolStartEvent t => WriteAsync("tool", new { name = t.Name }, ct),
        TextDeltaEvent d => WriteAsync("delta", new { text = d.Text }, ct),
        ResetEvent => WriteAsync("reset", new { }, ct),
        DoneEvent done => WriteAsync("done", done.Reply, ct),
        _ => Task.CompletedTask
    };

    private Task WriteAsync(string name, object data, CancellationToken ct) =>
        WriteRawAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, ApiJson.Options)}\n\n", ct);

    private async Task WriteRawAsync(string chunk, CancellationToken ct)
    {
        await Response.WriteAsync(chunk, ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>Danh sách hội thoại của tôi, mới nhất trước: limit (mặc định 30).</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] int limit = 30, CancellationToken cancellationToken = default) =>
        Success(await _assistant.ListConversationsAsync(limit, cancellationToken));

    /// <summary>Tin nhắn của một hội thoại (kèm thẻ bản nháp), cũ → mới.</summary>
    [HttpGet("conversations/{id:long}/messages")]
    public async Task<IActionResult> GetMessages(long id, CancellationToken cancellationToken) =>
        Success(await _assistant.GetMessagesAsync(id, cancellationToken));

    /// <summary>Bản nháp của tôi (trạng thái hiện tại, kể cả đã hết hạn).</summary>
    [HttpGet("drafts/{id:guid}")]
    public async Task<IActionResult> GetDraft(Guid id, CancellationToken cancellationToken) =>
        Success(DraftView.From(await _drafts.GetAsync(id, UserId, cancellationToken)));

    /// <summary>
    /// Xác nhận bản nháp → gửi đơn thật (đi qua đầy đủ kiểm tra hợp lệ như tạo đơn thủ công). Bấm lặp lại trả đơn đã gửi, không tạo trùng.
    /// Đơn yêu cầu minh chứng thì gửi kèm "file" (ảnh, multipart/form-data); không có thì để trống.
    /// </summary>
    [HttpPost("drafts/{id:guid}/confirm")]
    [Consumes("multipart/form-data", "application/json")]
    [RequestSizeLimit(MaxConfirmBytes)]
    public async Task<IActionResult> ConfirmDraft(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        var (draft, result) = file is null
            ? await _drafts.ConfirmAsync(id, UserId, null, cancellationToken)
            : await ConfirmWithFileAsync(id, file, cancellationToken);
        return Success(new { draft = DraftView.From(draft), requestId = result.RequestId }, "Đã gửi đơn, chờ xử lý.");
    }

    private async Task<(AssistantDraft, SubmissionResult)> ConfirmWithFileAsync(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await _drafts.ConfirmAsync(id, UserId,
            new SubmissionAttachment(stream, file.FileName, file.ContentType ?? string.Empty, file.Length), cancellationToken);
    }

    /// <summary>Hủy bản nháp (chưa gửi).</summary>
    [HttpPost("drafts/{id:guid}/cancel")]
    public async Task<IActionResult> CancelDraft(Guid id, CancellationToken cancellationToken) =>
        Success(DraftView.From(await _drafts.CancelAsync(id, UserId, cancellationToken)), "Đã hủy bản nháp.");
}
