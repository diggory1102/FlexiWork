using System.Text.Json.Nodes;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Attrition;

namespace FW.Application.Features.Assistant;

/// <summary>Đơn đang chờ duyệt — chỉ Quản lý (theo chi nhánh) / HR / Admin.</summary>
public sealed class GetPendingRequestsTool : IAssistantTool
{
    private readonly IHrDataService _hr;

    public GetPendingRequestsTool(IHrDataService hr) => _hr = hr;

    public string Name => "get_pending_requests";

    public string Description =>
        "(Quản lý/HR) Liệt kê đơn đang chờ duyệt: Quản lý thấy đơn của chi nhánh mình, HR/Admin thấy toàn công ty. Dùng cho: 'có đơn nào chờ em duyệt không'.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["limit"] = Schema.Int("Số đơn tối đa, mặc định 10, tối đa 30.")
    });

    public IReadOnlyCollection<string>? AllowedRoles => AiRoles.Managers;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var companyWide = ctx.Roles.Contains(RoleNames.Admin) || ctx.Roles.Contains(RoleNames.Hr);
        if (!companyWide && ctx.BranchId is null)
            return new ToolResult(new JsonObject { ["error"] = "Tài khoản Quản lý chưa gắn chi nhánh." });

        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 30);
        var items = await _hr.GetPendingRequestsAsync(companyWide ? null : ctx.BranchId, limit, ct);

        return new ToolResult(new JsonObject
        {
            ["scope"] = companyWide ? "toàn công ty" : "chi nhánh của bạn",
            ["count"] = items.Count,
            ["requests"] = new JsonArray(items.Select(r => (JsonNode)new JsonObject
            {
                ["id"] = r.Id,
                ["employee"] = r.EmployeeName,
                ["type"] = r.LeaveTypeName,
                ["start_date"] = r.StartDate.ToString("yyyy-MM-dd"),
                ["end_date"] = r.EndDate.ToString("yyyy-MM-dd"),
                ["days"] = r.DurationDays,
                ["reason"] = r.Reason
            }).ToArray())
        });
    }
}

/// <summary>Nhân sự cần quan tâm (cảnh báo nguy cơ nghỉ việc) — chỉ Quản lý/HR/Admin, đúng phạm vi chi nhánh.</summary>
public sealed class GetAttritionAlertsTool : IAssistantTool
{
    private readonly AttritionQueryService _query;

    public GetAttritionAlertsTool(AttritionQueryService query) => _query = query;

    public string Name => "get_attrition_alerts";

    public string Description =>
        "(Quản lý/HR) Xem danh sách nhân sự cần quan tâm do hệ thống chấm điểm nguy cơ nghỉ việc, kèm lý do. " +
        "Dùng cho: 'ai trong chi nhánh có dấu hiệu muốn nghỉ'. Chỉ là tín hiệu tham khảo, không dùng để kỷ luật.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["min_level"] = Schema.Str("Mức tối thiểu. Mặc định medium.", "medium", "high"),
        ["limit"] = Schema.Int("Số người tối đa, mặc định 10.")
    });

    public IReadOnlyCollection<string>? AllowedRoles => AiRoles.Managers;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var level = ToolArgs.Str(args, "min_level")?.ToLowerInvariant() switch
        {
            null or "medium" => RiskLevel.Medium,
            "high" => RiskLevel.High,
            var other => throw new ToolArgumentException($"min_level chỉ nhận medium | high, nhận được \"{other}\".")
        };
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 30);

        var caller = new AttritionCaller(ctx.UserId, ctx.BranchId, ctx.Roles);
        IReadOnlyList<AttritionSnapshot> alerts;
        try
        {
            alerts = await _query.GetAlertsAsync(caller, null, level, limit, ct);
        }
        catch (AppException ex)
        {
            return new ToolResult(new JsonObject { ["error"] = ex.Message });
        }

        return new ToolResult(new JsonObject
        {
            ["count"] = alerts.Count,
            ["note"] = "Tín hiệu tham khảo từ biến động chấm công/đơn từ, không phải kết luận và không dùng để kỷ luật.",
            ["employees"] = new JsonArray(alerts.Select(a => (JsonNode)new JsonObject
            {
                ["name"] = a.EmployeeName,
                ["score"] = a.Score,
                ["level"] = a.Level == RiskLevel.High ? "cao" : "trung bình",
                ["change_vs_last_week"] = a.Delta,
                ["reasons"] = new JsonArray(a.Reasons.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray())
            }).ToArray())
        });
    }
}
