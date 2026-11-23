
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

public sealed record AssistantConversation(long Id, int UserId, string Title, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>Role: user | assistant. <see cref="ToolTrace"/> là JSON tóm tắt các công cụ đã gọi (để kiểm toán).</summary>
public sealed record StoredMessage(
    long Id,
    long ConversationId,
    string Role,
    string Content,
    string? ToolTrace,
    IReadOnlyList<Guid> DraftIds,
    DateTime CreatedAt);

public interface IAssistantStore
{
    Task<AssistantConversation> CreateConversationAsync(int userId, string title, DateTime now, CancellationToken ct = default);

    /// <summary>Chỉ trả hội thoại thuộc <paramref name="userId"/>; của người khác → null.</summary>
    Task<AssistantConversation?> GetConversationAsync(long conversationId, int userId, CancellationToken ct = default);

    Task<IReadOnlyList<AssistantConversation>> ListConversationsAsync(int userId, int limit, CancellationToken ct = default);

    Task<StoredMessage> AddMessageAsync(
        long conversationId, string role, string content, string? toolTrace,
        IReadOnlyList<Guid> draftIds, DateTime now, CancellationToken ct = default);

    /// <summary>Các tin gần nhất, sắp theo thời gian tăng dần.</summary>
    Task<IReadOnlyList<StoredMessage>> GetRecentMessagesAsync(long conversationId, int limit, CancellationToken ct = default);

    /// <summary>Số tin nhắn người dùng đã gửi trong ngày (giờ Việt Nam) — dùng giới hạn chi phí.</summary>
    Task<int> CountUserMessagesOnAsync(int userId, DateOnly date, CancellationToken ct = default);
}

public enum DraftStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Expired
}

public sealed record DraftLine(string Label, string Value);

/// <summary>Nội dung thẻ xác nhận hiển thị cho người dùng.</summary>
public sealed record DraftCard(
    string Kind,
    string Title,
    IReadOnlyList<DraftLine> Lines,
    IReadOnlyList<string> Warnings,
    bool RequiresAttachment);

/// <summary>Bản nháp đơn do trợ lý tạo — chưa phải đơn thật cho tới khi người dùng xác nhận.</summary>
public sealed class AssistantDraft
{
    public Guid Id { get; init; }
    public int UserId { get; init; }
    public long? ConversationId { get; init; }
    public required DraftCard Card { get; init; }
    public required LeaveRequestSubmission Submission { get; init; }
    public DraftStatus Status { get; set; } = DraftStatus.Pending;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }

    /// <summary>Mã đơn thật sau khi xác nhận.</summary>
    public int? RequestId { get; set; }
}

public interface IDraftStore
{
    Task SaveAsync(AssistantDraft draft, CancellationToken ct = default);
    Task<AssistantDraft?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Đổi trạng thái NGUYÊN TỬ (chỉ khi đang ở <paramref name="from"/>) — chống bấm "Xác nhận" hai lần gửi trùng đơn.
    /// Trả false nếu trạng thái hiện tại khác <paramref name="from"/>.
    /// </summary>
    Task<bool> TryTransitionAsync(Guid id, DraftStatus from, DraftStatus to, CancellationToken ct = default);

    /// <summary>Ghi mã đơn thật sau khi gửi thành công.</summary>
    Task SetRequestIdAsync(Guid id, int requestId, CancellationToken ct = default);
}
