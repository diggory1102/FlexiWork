using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Chat;

public record ChatUserDto(int Id, string FullName, string? AvatarUrl);

/// <param name="Preview">Nội dung rút gọn để hiển thị ở danh sách: văn bản, hoặc "[Hình ảnh]" nếu chỉ có ảnh.</param>
public record LastMessageDto(long Id, int SenderId, string SenderName, string Preview, DateTime CreatedAt);

/// <summary>Một phòng chat trong danh sách của tôi. Chat trực tiếp: <see cref="Title"/> / <see cref="AvatarUrl"/> là của người kia.</summary>
/// <param name="IsCustomGroup">Nhóm tùy chỉnh (UC 7.2): đổi tên / thêm / xóa thành viên được. Nhóm chi nhánh do hệ thống quản lý.</param>
/// <param name="OwnerId">Quản trị nhóm tùy chỉnh (người tạo, hoặc người kế nhiệm khi người tạo rời nhóm); null với phòng khác.</param>
public record ConversationDto(
    int Id,
    string Type,
    string Title,
    string? AvatarUrl,
    int? BranchId,
    int MemberCount,
    ChatUserDto? OtherUser,
    LastMessageDto? LastMessage,
    int UnreadCount,
    DateTime LastActivityAt,
    bool IsCustomGroup,
    int? OwnerId)
{
    public static ConversationDto From(ConversationRow r)
    {
        var isDirect = r.Type == ConversationType.Direct;
        var other = isDirect && r.OtherUserId is { } id ? new ChatUserDto(id, r.OtherFullName ?? "—", r.OtherAvatarUrl) : null;
        var last = r.LastMessageId is { } messageId
            ? new LastMessageDto(messageId, r.LastSenderId ?? 0, r.LastSenderName ?? "—", Preview(r.LastContent, r.LastHasImage), r.LastMessageAt ?? r.CreatedAt)
            : null;
        return new ConversationDto(r.Id, r.Type, isDirect ? other?.FullName ?? "Người dùng" : r.Title ?? "Nhóm chat", other?.AvatarUrl,
            r.BranchId, r.MemberCount, other, last, r.UnreadCount, r.LastMessageAt ?? r.CreatedAt,
            r.Type == ConversationType.Group && r.BranchId is null, r.CreatedBy);
    }

    public static string Preview(string? content, bool hasImage)
    {
        var text = (content ?? string.Empty).ReplaceLineEndings(" ").Trim();
        if (text.Length > 80) text = text[..79] + "…";
        return hasImage ? (text.Length == 0 ? "[Hình ảnh]" : $"[Hình ảnh] {text}") : text;
    }
}

public record ChatMemberDto(int UserId, string EmployeeCode, string FullName, string? AvatarUrl, string? PositionTitle);

public record ConversationDetailDto(ConversationDto Conversation, IReadOnlyList<ChatMemberDto> Members);

/// <summary>Một tin nhắn. Tin đã thu hồi: <see cref="IsDeleted"/> = true, nội dung và ảnh bị ẩn.</summary>
public record ChatMessageDto(
    long Id, int ConversationId, int SenderId, string SenderName, string? SenderAvatarUrl, string? Content, string? ImageUrl, bool IsDeleted, DateTime CreatedAt)
{
    public static ChatMessageDto From(MessageRow m) => new(
        m.Id, m.ConversationId, m.SenderId, m.SenderName, m.SenderAvatarUrl,
        m.IsDeleted ? null : m.Content, m.IsDeleted ? null : m.ImageUrl, m.IsDeleted, m.CreatedAt);
}

/// <summary>Một trang lịch sử (tin cũ → mới). Tải trang cũ hơn bằng <c>beforeId = NextBeforeId</c>.</summary>
public record MessagePageDto(IReadOnlyList<ChatMessageDto> Items, bool HasMore, long? NextBeforeId);

public class SendMessageRequest
{
    [Required(ErrorMessage = "Vui lòng nhập nội dung tin nhắn.")]
    [MaxLength(ChatService.MaxContentLength, ErrorMessage = "Tin nhắn tối đa 2000 ký tự.")]
    public string Content { get; set; } = string.Empty;
}

public class CreateGroupRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên nhóm.")]
    [StringLength(ChatService.MaxGroupTitleLength, MinimumLength = 2, ErrorMessage = "Tên nhóm từ 2 đến 100 ký tự.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Các thành viên ban đầu (không cần ghi người tạo — luôn có trong nhóm).</summary>
    [Required(ErrorMessage = "Vui lòng chọn thành viên.")]
    public List<int> MemberIds { get; set; } = new();
}

public class RenameGroupRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên nhóm.")]
    [StringLength(ChatService.MaxGroupTitleLength, MinimumLength = 2, ErrorMessage = "Tên nhóm từ 2 đến 100 ký tự.")]
    public string Title { get; set; } = string.Empty;
}

public class AddMembersRequest
{
    [Required(ErrorMessage = "Vui lòng chọn thành viên.")]
    public List<int> UserIds { get; set; } = new();
}

public class OpenDirectRequest
{
    [Required(ErrorMessage = "Vui lòng chọn người cần nhắn tin.")]
    public int? UserId { get; set; }
}

public class MarkReadRequest
{
    /// <summary>Đã đọc tới tin này; bỏ trống = tin mới nhất.</summary>
    public long? UpToMessageId { get; set; }
}

public record ChatUnreadDto(int ConversationId, int UnreadCount);

public record ChatTotalUnreadDto(int Count);

public class ChatContactFilter : PagedQuery
{
}

/// <summary>Người có thể nhắn tin (mọi nhân sự đang hoạt động trừ chính mình).</summary>
public record ChatContactDto(int Id, string EmployeeCode, string FullName, string? AvatarUrl, string? BranchName, string? PositionTitle);
