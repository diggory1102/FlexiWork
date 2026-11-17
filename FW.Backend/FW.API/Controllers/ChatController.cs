using FW.Application.Common.Exceptions;
using FW.Application.Features.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Chat nội bộ (UC 7.1, 7.3): mọi người dùng đăng nhập đều dùng được; chỉ thành viên mới đọc / gửi trong một phòng. Tin nhắn mới được đẩy tức thì qua
/// SignalR tại /hubs/chat (sự kiện "ReceiveMessage", "MessageDeleted", "ConversationRead").
/// </summary>
[Authorize]
public class ChatController : ApiControllerBase
{
    private const long MaxRequestBytes = ChatService.MaxImageBytes + 64 * 1024;

    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>Phòng chat của tôi (hoạt động gần nhất trước), kèm tin mới nhất và số chưa đọc. Đồng bộ luôn nhóm chat chi nhánh của tôi.</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken) =>
        Success(await _chatService.GetConversationsAsync(cancellationToken));

    /// <summary>Chi tiết một phòng kèm danh sách thành viên.</summary>
    [HttpGet("conversations/{id:int}")]
    public async Task<IActionResult> GetConversation(int id, CancellationToken cancellationToken) =>
        Success(await _chatService.GetConversationAsync(id, cancellationToken));

    /// <summary>Mở chat trực tiếp với một người: {userId} — dùng lại phòng đã có.</summary>
    [HttpPost("conversations/direct")]
    public async Task<IActionResult> OpenDirect([FromBody] OpenDirectRequest request, CancellationToken cancellationToken) =>
        Success(await _chatService.GetOrCreateDirectAsync(request.UserId ?? 0, cancellationToken));

    /// <summary>Lịch sử tin nhắn (cũ → mới). Trang cũ hơn: beforeId = nextBeforeId của trang trước; pageSize mặc định 30, tối đa 100.</summary>
    [HttpGet("conversations/{id:int}/messages")]
    public async Task<IActionResult> GetMessages(int id, [FromQuery] long? beforeId, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        Success(await _chatService.GetMessagesAsync(id, beforeId, pageSize, cancellationToken));

    /// <summary>Gửi tin nhắn văn bản: {content} (tối đa 2000 ký tự).</summary>
    [HttpPost("conversations/{id:int}/messages")]
    public async Task<IActionResult> SendText(int id, [FromBody] SendMessageRequest request, CancellationToken cancellationToken) =>
        Created(await _chatService.SendTextAsync(id, request, cancellationToken), "Đã gửi tin nhắn.");

    /// <summary>Gửi ảnh (form-data): "file" = ảnh JPG / PNG / WEBP / HEIC ≤ 5 MB, "caption" = chú thích (không bắt buộc).</summary>
    [HttpPost("conversations/{id:int}/messages/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> SendImage(int id, [FromForm] string? caption, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null) throw new BadRequestException("Vui lòng chọn ảnh (trường form-data \"file\").");
        await using var stream = file.OpenReadStream();
        var message = await _chatService.SendImageAsync(id, caption, new ChatImage(stream, file.FileName, file.ContentType ?? string.Empty, file.Length), cancellationToken);
        return Created(message, "Đã gửi ảnh.");
    }

    /// <summary>Đánh dấu đã đọc: {upToMessageId} (bỏ trống = tới tin mới nhất). Trả về số tin chưa đọc còn lại.</summary>
    [HttpPost("conversations/{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, [FromBody] MarkReadRequest? request, CancellationToken cancellationToken) =>
        Success(await _chatService.MarkReadAsync(id, request?.UpToMessageId, cancellationToken));

    /// <summary>Tạo nhóm chat tùy chỉnh (UC 7.2): {title, memberIds}. Người tạo là quản trị nhóm.</summary>
    [HttpPost("conversations/group")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken cancellationToken) =>
        Created(await _chatService.CreateGroupAsync(request, cancellationToken), "Đã tạo nhóm chat.");

    /// <summary>Đổi tên nhóm tùy chỉnh (quản trị nhóm): {title}.</summary>
    [HttpPut("conversations/{id:int}")]
    public async Task<IActionResult> RenameGroup(int id, [FromBody] RenameGroupRequest request, CancellationToken cancellationToken) =>
        Success(await _chatService.RenameGroupAsync(id, request, cancellationToken), "Đã đổi tên nhóm.");

    /// <summary>Thêm thành viên vào nhóm tùy chỉnh (quản trị nhóm): {userIds}.</summary>
    [HttpPost("conversations/{id:int}/members")]
    public async Task<IActionResult> AddMembers(int id, [FromBody] AddMembersRequest request, CancellationToken cancellationToken) =>
        Success(await _chatService.AddMembersAsync(id, request, cancellationToken), "Đã thêm thành viên.");

    /// <summary>Xóa thành viên khỏi nhóm (quản trị nhóm), hoặc tự rời nhóm khi userId là chính mình.</summary>
    [HttpDelete("conversations/{id:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int userId, CancellationToken cancellationToken)
    {
        await _chatService.RemoveMemberAsync(id, userId, cancellationToken);
        return Success("Đã cập nhật thành viên nhóm.");
    }

    /// <summary>Thu hồi tin nhắn của chính mình.</summary>
    [HttpDelete("messages/{id:long}")]
    public async Task<IActionResult> DeleteMessage(long id, CancellationToken cancellationToken)
    {
        await _chatService.DeleteMessageAsync(id, cancellationToken);
        return Success("Đã thu hồi tin nhắn.");
    }

    /// <summary>Tổng số tin chưa đọc của tôi (cho huy hiệu trên tab Tin nhắn).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken) =>
        Success(await _chatService.GetUnreadTotalAsync(cancellationToken));

    /// <summary>Tìm người để nhắn tin (mọi nhân sự đang hoạt động trừ chính mình); search theo tên / mã / email.</summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] ChatContactFilter filter, CancellationToken cancellationToken) =>
        Success(await _chatService.GetContactsAsync(filter, cancellationToken));
}
