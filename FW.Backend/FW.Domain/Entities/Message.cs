using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>Một tin nhắn (bảng messages): văn bản, ảnh hoặc cả hai. Thu hồi = xóa mềm (<see cref="IsDeleted"/>).</summary>
public class Message : BaseEntity<long>, ISoftDelete
{
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImagePublicId { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
}
