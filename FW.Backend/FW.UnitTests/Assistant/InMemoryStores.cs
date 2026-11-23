using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using System.Collections.Concurrent;

namespace FW.UnitTests.Assistant;

public sealed class InMemoryAssistantStore : IAssistantStore
{
    private readonly object _gate = new();
    private readonly List<AssistantConversation> _conversations = new();
    private readonly List<StoredMessage> _messages = new();
    private long _nextConversation = 1;
    private long _nextMessage = 1;

    public Task<AssistantConversation> CreateConversationAsync(int userId, string title, DateTime now, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var c = new AssistantConversation(_nextConversation++, userId, title, now, now);
            _conversations.Add(c);
            return Task.FromResult(c);
        }
    }

    public Task<AssistantConversation?> GetConversationAsync(long conversationId, int userId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_conversations.FirstOrDefault(c => c.Id == conversationId && c.UserId == userId));
    }

    public Task<IReadOnlyList<AssistantConversation>> ListConversationsAsync(int userId, int limit, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<AssistantConversation>>(
                _conversations.Where(c => c.UserId == userId).OrderByDescending(c => c.UpdatedAt).Take(limit).ToList());
    }

    public Task<StoredMessage> AddMessageAsync(
        long conversationId, string role, string content, string? toolTrace,
        IReadOnlyList<Guid> draftIds, DateTime now, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var m = new StoredMessage(_nextMessage++, conversationId, role, content, toolTrace, draftIds, now);
            _messages.Add(m);
            var i = _conversations.FindIndex(c => c.Id == conversationId);
            if (i >= 0) _conversations[i] = _conversations[i] with { UpdatedAt = now };
            return Task.FromResult(m);
        }
    }

    public Task<IReadOnlyList<StoredMessage>> GetRecentMessagesAsync(long conversationId, int limit, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<StoredMessage>>(
                _messages.Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.Id).Take(limit).Reverse().ToList());
    }

    public Task<int> CountUserMessagesOnAsync(int userId, DateOnly date, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var owned = _conversations.Where(c => c.UserId == userId).Select(c => c.Id).ToHashSet();
            return Task.FromResult(_messages.Count(m => m.Role == "user" && owned.Contains(m.ConversationId) && DateOnly.FromDateTime(m.CreatedAt) == date));
        }
    }
}

public sealed class InMemoryDraftStore : IDraftStore
{
    private readonly ConcurrentDictionary<Guid, AssistantDraft> _drafts = new();
    private readonly object _gate = new();

    public Task SaveAsync(AssistantDraft draft, CancellationToken ct = default)
    {
        _drafts[draft.Id] = draft;
        return Task.CompletedTask;
    }

    public Task<AssistantDraft?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_drafts.TryGetValue(id, out var d) ? d : null);

    public Task<bool> TryTransitionAsync(Guid id, DraftStatus from, DraftStatus to, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_drafts.TryGetValue(id, out var d) || d.Status != from) return Task.FromResult(false);
            d.Status = to;
            return Task.FromResult(true);
        }
    }

    public Task SetRequestIdAsync(Guid id, int requestId, CancellationToken ct = default)
    {
        if (_drafts.TryGetValue(id, out var d)) d.RequestId = requestId;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryAttritionData : IAttritionDataSource, IAttritionSnapshotStore
{
    private readonly object _gate = new();
    private readonly List<AttritionSnapshot> _snapshots = new();

    /// <summary>Hoạt động nhân viên — gán trực tiếp khi dựng dữ liệu mẫu / kiểm thử.</summary>
    public List<EmployeeActivity> Activities { get; } = new();

    public Task<IReadOnlyList<EmployeeActivity>> GetActivitiesAsync(DateOnly from, DateOnly toExclusive, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EmployeeActivity>>(Activities.ToList());

    public Task UpsertAsync(IReadOnlyList<AttritionSnapshot> snapshots, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var s in snapshots)
            {
                _snapshots.RemoveAll(x => x.UserId == s.UserId && x.WeekStart == s.WeekStart);
                _snapshots.Add(s);
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AttritionSnapshot>> GetLatestAsync(int? branchId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var latest = _snapshots
                .GroupBy(s => s.UserId)
                .Select(g => g.OrderByDescending(s => s.WeekStart).First())
                .Where(s => branchId is null || s.BranchId == branchId)
                .ToList();
            return Task.FromResult<IReadOnlyList<AttritionSnapshot>>(latest);
        }
    }

    public Task<IReadOnlyList<AttritionSnapshot>> GetHistoryAsync(int userId, int weeks, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _snapshots.Where(s => s.UserId == userId).OrderByDescending(s => s.WeekStart).Take(weeks).Reverse().ToList();
            return Task.FromResult<IReadOnlyList<AttritionSnapshot>>(list);
        }
    }
}
