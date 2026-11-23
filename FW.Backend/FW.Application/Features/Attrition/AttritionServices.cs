using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;

namespace FW.Application.Features.Attrition;

/// <summary>Chấm điểm toàn bộ nhân viên cho một tuần và lưu snapshot (UC 10.3).</summary>
public sealed class AttritionScoringService
{
    private readonly IAttritionDataSource _source;
    private readonly IAttritionSnapshotStore _store;
    private readonly IDateTimeProvider _clock;
    private readonly AttritionOptions _options;

    public AttritionScoringService(
        IAttritionDataSource source,
        IAttritionSnapshotStore store,
        IDateTimeProvider clock,
        IOptions<AttritionOptions> options)
    {
        _source = source;
        _store = store;
        _clock = clock;
        _options = options.Value;
    }

    /// <param name="weekStart">Thứ Hai của tuần cần chấm (cửa sổ gần = 2 tuần ngay TRƯỚC ngày này). Null = tuần hiện tại.</param>
    public async Task<AttritionRunSummary> RunAsync(DateOnly? weekStart = null, CancellationToken ct = default)
    {
        var asOf = RelativeDateResolver.StartOfWeek(weekStart ?? _clock.Today);
        var from = asOf.AddDays(-7 * (_options.RecentWeeks + _options.BaselineWeeks));

        var activities = await _source.GetActivitiesAsync(from, asOf, ct);

        // Điểm tuần trước để hiện xu hướng; nếu chạy lại đúng tuần này thì giữ nguyên "tuần trước" đã có
        var latest = (await _store.GetLatestAsync(null, ct)).ToDictionary(s => s.UserId);
        var now = _clock.Now;

        var snapshots = new List<AttritionSnapshot>(activities.Count);
        foreach (var a in activities)
        {
            var assessment = AttritionScorer.Assess(a, asOf, _options);
            int? previous = null;
            if (latest.TryGetValue(a.UserId, out var prev))
                previous = prev.WeekStart == asOf ? prev.PreviousScore : (prev.DataSufficient ? prev.Score : null);

            snapshots.Add(new AttritionSnapshot(
                a.UserId, a.Name, a.BranchId, asOf,
                assessment.Score, assessment.Level, assessment.DataSufficient, previous,
                assessment.Reasons, assessment.Signals, now));
        }

        await _store.UpsertAsync(snapshots, ct);

        return new AttritionRunSummary(
            asOf,
            snapshots.Count,
            snapshots.Count(s => s.DataSufficient),
            snapshots.Count(s => s.DataSufficient && s.Level == RiskLevel.Medium),
            snapshots.Count(s => s.DataSufficient && s.Level == RiskLevel.High));
    }
}

/// <summary>Truy vấn cảnh báo cho Quản lý / HR (UC 10.4) — có kiểm soát phạm vi theo vai trò.</summary>
public sealed class AttritionQueryService
{
    private readonly IAttritionSnapshotStore _store;

    public AttritionQueryService(IAttritionSnapshotStore store) => _store = store;

    public async Task<IReadOnlyList<AttritionSnapshot>> GetAlertsAsync(
        AttritionCaller caller, int? branchId, RiskLevel minLevel, int limit, CancellationToken ct = default)
    {
        var scope = ResolveBranchScope(caller, branchId);
        var all = await _store.GetLatestAsync(scope, ct);
        return all
            .Where(s => s.DataSufficient && s.Level >= minLevel)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.EmployeeName)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
    }

    /// <summary>Số nhân viên theo mức nguy cơ của lần chấm gần nhất trong phạm vi của người xem (ô cảnh báo ở Dashboard).</summary>
    public async Task<AttritionSummaryDto> GetSummaryAsync(AttritionCaller caller, int? branchId, CancellationToken ct = default)
    {
        var scope = ResolveBranchScope(caller, branchId);
        var scored = (await _store.GetLatestAsync(scope, ct)).Where(s => s.DataSufficient).ToList();
        if (scored.Count == 0) return new AttritionSummaryDto(null, null, 0, 0, 0);
        return new AttritionSummaryDto(
            scored.Max(s => s.WeekStart), scored.Max(s => s.ComputedAt), scored.Count,
            scored.Count(s => s.Level == RiskLevel.Medium), scored.Count(s => s.Level == RiskLevel.High));
    }

    public async Task<IReadOnlyList<AttritionSnapshot>> GetTrendAsync(
        AttritionCaller caller, int userId, int weeks, CancellationToken ct = default)
    {
        EnsureManager(caller);
        var history = await _store.GetHistoryAsync(userId, Math.Clamp(weeks, 1, 52), ct);
        if (history.Count == 0) return history;

        // Quản lý chỉ xem nhân viên thuộc chi nhánh mình; không lộ sự tồn tại của người ngoài phạm vi
        if (IsBranchScoped(caller) && history[^1].BranchId != caller.BranchId)
            throw new NotFoundException("Không tìm thấy dữ liệu của nhân viên này.");
        return history;
    }

    private static int? ResolveBranchScope(AttritionCaller caller, int? requested)
    {
        EnsureManager(caller);
        if (!IsBranchScoped(caller)) return requested;

        if (caller.BranchId is null)
            throw new ForbiddenException("Tài khoản Quản lý chưa gắn chi nhánh.");
        if (requested is not null && requested != caller.BranchId)
            throw new ForbiddenException("Bạn chỉ xem được nhân viên thuộc chi nhánh của mình.");
        return caller.BranchId;
    }

    private static void EnsureManager(AttritionCaller caller)
    {
        if (!caller.Roles.Any(r => AiRoles.Managers.Contains(r)))
            throw new ForbiddenException("Chỉ Quản lý / HR được xem cảnh báo nguy cơ nghỉ việc.");
    }

    /// <summary>Manager (không kiêm Admin/HR) bị giới hạn theo chi nhánh.</summary>
    private static bool IsBranchScoped(AttritionCaller caller) =>
        !caller.Roles.Contains(RoleNames.Admin) && !caller.Roles.Contains(RoleNames.Hr);
}

/// <summary>[Tùy chọn — Task 7.5] Nhờ LLM viết đoạn tóm tắt dễ đọc cho Quản lý; lỗi mô hình thì dùng bản dựng sẵn.</summary>
public sealed class AttritionNarrator
{
    private const string SystemPrompt =
        "Bạn là trợ lý nhân sự, viết ghi chú ngắn (3–4 câu, tiếng Việt) cho Quản lý về một nhân viên có tín hiệu cần quan tâm. " +
        "QUY TẮC: chỉ dùng các dữ kiện được cung cấp, không suy đoán nguyên nhân hay kết luận nhân viên sẽ nghỉ việc, " +
        "không nhắc kỷ luật/sa thải; giọng trung lập, gợi ý một buổi trao đổi thân thiện để hiểu khó khăn của nhân viên. " +
        "Đây chỉ là tín hiệu tham khảo.";

    private readonly ILlmChatClient _llm;
    private readonly ILogger<AttritionNarrator> _logger;

    public AttritionNarrator(ILlmChatClient llm, ILogger<AttritionNarrator> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<string> SummarizeAsync(AttritionSnapshot s, CancellationToken ct = default)
    {
        var fallback = BuildFallback(s);
        if (!s.DataSufficient || s.Reasons.Count == 0) return fallback;

        var facts = $"Nhân viên: {s.EmployeeName}\nĐiểm nguy cơ: {s.Score}/100\n" +
                    (s.Delta is { } d ? $"Thay đổi so với tuần trước: {d:+#;-#;0}\n" : string.Empty) +
                    "Các biến động ghi nhận:\n" + string.Join("\n", s.Reasons.Select(r => "- " + r));
        try
        {
            var res = await _llm.ChatAsync(
                new[] { LlmMessage.System(SystemPrompt), LlmMessage.User(facts) }, Array.Empty<LlmToolDefinition>(), ct);
            return string.IsNullOrWhiteSpace(res.Content) ? fallback : res.Content;
        }
        catch (LlmUnavailableException ex)
        {
            _logger.LogWarning(ex, "Không tóm tắt được bằng LLM, dùng bản dựng sẵn.");
            return fallback;
        }
    }

    public static string BuildFallback(AttritionSnapshot s)
    {
        if (!s.DataSufficient)
            return $"Chưa đủ dữ liệu chấm công của {s.EmployeeName} để đánh giá (cần thêm vài tuần làm việc).";
        if (s.Reasons.Count == 0)
            return $"{s.EmployeeName}: chưa ghi nhận biến động đáng chú ý so với thói quen của chính nhân viên.";
        return $"{s.EmployeeName} (điểm {s.Score}/100): " + string.Join("; ", s.Reasons) +
               ". Đây chỉ là tín hiệu tham khảo — nên trao đổi thân thiện để hiểu thêm.";
    }
}
