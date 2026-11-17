using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>Một phòng chat (bảng conversations): chat trực tiếp 1-1 hoặc nhóm chi nhánh tự động.</summary>
public class Conversation : BaseEntity, ISoftDelete
{
    /// <summary>Giá trị thuộc <see cref="ConversationType"/>.</summary>
    public string Type { get; set; } = ConversationType.Direct;

    /// <summary>Tên nhóm (chat trực tiếp để trống — hiển thị tên người kia).</summary>
    public string? Title { get; set; }

    /// <summary>Chi nhánh của nhóm chat chi nhánh.</summary>
    public int? BranchId { get; set; }

    /// <summary>Khóa duy nhất chống tạo trùng phòng chat: "direct:{id nhỏ}:{id lớn}" hoặc "branch:{id}".</summary>
    public string? UniqueKey { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }

    public static string DirectKey(int userA, int userB) => $"direct:{Math.Min(userA, userB)}:{Math.Max(userA, userB)}";

    public static string BranchKey(int branchId) => $"branch:{branchId}";
}
