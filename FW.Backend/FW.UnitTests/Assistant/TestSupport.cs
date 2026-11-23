using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FW.UnitTests.Assistant;

public sealed class TestClock : IDateTimeProvider
{
    public DateTime Now { get; set; }
    public DateOnly Today => DateOnly.FromDateTime(Now);
    public DateTime UtcNow => Now.AddHours(-7);

    /// <summary>Thứ Tư 18/11/2026 10:00 → "thứ 5 tuần này" = 19/11/2026.</summary>
    public static TestClock Wednesday() => new() { Now = new DateTime(2026, 11, 18, 10, 0, 0) };
}

public sealed class TestUser : ICurrentUserService
{
    public int? UserId { get; set; }
    public int? BranchId { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsAuthenticated => UserId is not null;
    public bool IsInRole(string role) => Roles.Contains(role);

    public static TestUser Employee(int id = 3) => new() { UserId = id, BranchId = 1, Roles = new[] { RoleNames.Employee } };
    public static TestUser Manager(int id = 2, int branch = 1) => new() { UserId = id, BranchId = branch, Roles = new[] { RoleNames.Manager } };
    public static TestUser Hr() => new() { UserId = 4, BranchId = null, Roles = new[] { RoleNames.Hr } };
}

/// <summary>LLM giả: trả lần lượt các phản hồi đã xếp sẵn và ghi lại những gì được gửi lên.</summary>
public sealed class ScriptedLlm : ILlmChatClient
{
    private readonly Queue<Func<IReadOnlyList<LlmMessage>, LlmResponse>> _script = new();
    public List<(IReadOnlyList<LlmMessage> Messages, IReadOnlyList<LlmToolDefinition> Tools)> Calls { get; } = new();
    public Exception? ThrowOnCall { get; set; }

    /// <summary>Nếu hết kịch bản thì lặp lại phản hồi này (dùng để thử vòng lặp vô hạn).</summary>
    public Func<IReadOnlyList<LlmMessage>, LlmResponse>? Fallback { get; set; }

    public ScriptedLlm Say(string text)
    {
        _script.Enqueue(_ => new LlmResponse(text, Array.Empty<LlmToolCall>()));
        return this;
    }

    public ScriptedLlm CallTool(string name, string argsJson)
    {
        _script.Enqueue(_ => new LlmResponse(string.Empty, new[]
        {
            new LlmToolCall(name, System.Text.Json.Nodes.JsonNode.Parse(argsJson)!.AsObject())
        }));
        return this;
    }

    public async IAsyncEnumerable<LlmStreamEvent> ChatStreamAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await ChatAsync(messages, tools, cancellationToken);
        // Phát câu trả lời thành vài đoạn để thử ghép luồng
        foreach (var piece in response.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            yield return new LlmTextDelta(piece + " ");
        yield return new LlmCompleted(response);
    }

    public Task<LlmResponse> ChatAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, CancellationToken cancellationToken = default)
    {
        Calls.Add((messages.ToList(), tools.ToList())); // sao chép vì dịch vụ còn thêm vào danh sách sau lần gọi
        if (ThrowOnCall is not null) throw ThrowOnCall;

        if (_script.Count > 0) return Task.FromResult(_script.Dequeue()(messages));
        if (Fallback is not null) return Task.FromResult(Fallback(messages));
        throw new InvalidOperationException("ScriptedLlm hết kịch bản.");
    }
}

/// <summary>Bộ dữ liệu + dịch vụ dựng sẵn cho test, mặc định nhân viên Lê Văn Nhân Viên (id 3) vào Thứ Tư 18/11/2026.</summary>
public sealed class World
{
    public TestClock Clock { get; } = TestClock.Wednesday();
    public TestUser User { get; set; } = TestUser.Employee();
    public InMemoryHrData Hr { get; } = new();
    public InMemoryAttritionData Attrition { get; } = new();
    public InMemoryAssistantStore Store { get; } = new();
    public InMemoryDraftStore Drafts { get; } = new();
    public ScriptedLlm Llm { get; } = new();
    public AssistantOptions Options { get; } = new();
    public AttritionOptions AttritionOptions { get; } = new();

    public World()
    {
        DemoDataSeeder.Seed(Hr, Attrition, Clock.Today);
    }

    public DraftService NewDraftService(ILeaveRequestSubmitter? submitter = null) =>
        new(Hr, Drafts, submitter ?? Hr, Clock, Microsoft.Extensions.Options.Options.Create(Options));

    public AttritionScoringService NewScoring() =>
        new(Attrition, Attrition, Clock, Microsoft.Extensions.Options.Options.Create(AttritionOptions));

    public AttritionQueryService NewQuery() => new(Attrition);

    public AssistantService NewAssistant(ILlmChatClient? llm = null)
    {
        var draftService = NewDraftService();
        var tools = new IAssistantTool[]
        {
            new GetMyScheduleTool(Hr),
            new GetMyTimesheetTool(Hr),
            new GetMyLeaveBalanceTool(Hr),
            new GetMyRequestsTool(Hr),
            new DraftLeaveRequestTool(draftService),
            new DraftShiftSwapTool(draftService),
            new DraftOvertimeRequestTool(draftService),
            new DraftExplanationRequestTool(draftService),
            new GetPendingRequestsTool(Hr),
            new GetAttritionAlertsTool(NewQuery())
        };
        return new AssistantService(
            llm ?? Llm, tools, Store, Drafts, Hr, User, Clock,
            Microsoft.Extensions.Options.Options.Create(Options), NullLogger<AssistantService>.Instance);
    }
}
