using FW.Application.Features.Notifications;

namespace FW.IntegrationTests.Notifications;

/// <summary>Ghi lại các thông báo mà nghiệp vụ gửi đi để test kiểm tra người nhận và nội dung (không ghi CSDL).</summary>
public sealed class RecordingNotifier : INotifier
{
    /// <param name="ToReviewers">true = gửi cho người duyệt của chi nhánh <see cref="BranchId"/> (người tạo <see cref="Requester"/> bị loại).</param>
    public sealed record Sent(IReadOnlyList<int> Users, string Type, string Title, string Content, string? Reference, bool ToReviewers = false,
        int? BranchId = null, int? Requester = null);

    public List<Sent> Items { get; } = new();

    public Task NotifyAsync(IEnumerable<int> userIds, string type, string title, string content, string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        Items.Add(new Sent(userIds.Distinct().ToList(), type, title, content, referenceId));
        return Task.CompletedTask;
    }

    public Task NotifyReviewersAsync(int? branchId, int requesterId, string type, string title, string content, string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        Items.Add(new Sent(Array.Empty<int>(), type, title, content, referenceId, ToReviewers: true, BranchId: branchId, Requester: requesterId));
        return Task.CompletedTask;
    }

    /// <summary>Thông báo gửi riêng cho một người dùng.</summary>
    public IEnumerable<Sent> To(int userId) => Items.Where(i => i.Users.Contains(userId));

    /// <summary>Thông báo gửi cho người duyệt.</summary>
    public IEnumerable<Sent> ToReviewers => Items.Where(i => i.ToReviewers);
}
