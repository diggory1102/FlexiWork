using System.Text.Json.Nodes;
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

/// <summary>Lịch ca của chính người hỏi.</summary>
public sealed class GetMyScheduleTool : IAssistantTool
{
    private const int MaxSpanDays = 62;
    private readonly IHrDataService _hr;

    public GetMyScheduleTool(IHrDataService hr) => _hr = hr;

    public string Name => "get_my_schedule";

    public string Description =>
        "Xem lịch ca làm đã công bố của CHÍNH người đang hỏi trong một khoảng ngày. Dùng cho: 'tuần sau em làm ca nào', 'mai em có ca không'.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["from_date"] = Schema.Str("Ngày bắt đầu, dạng yyyy-MM-dd. Bỏ trống = hôm nay."),
        ["to_date"] = Schema.Str("Ngày kết thúc, dạng yyyy-MM-dd. Bỏ trống = 6 ngày sau from_date.")
    });

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var from = ToolArgs.Date(args, "from_date") ?? ctx.Today;
        var to = ToolArgs.Date(args, "to_date") ?? from.AddDays(6);
        if (to < from) throw new ToolArgumentException("to_date phải sau hoặc bằng from_date.");
        if (to.DayNumber - from.DayNumber + 1 > MaxSpanDays)
            throw new ToolArgumentException($"Chỉ xem tối đa {MaxSpanDays} ngày mỗi lần.");

        var shifts = await _hr.GetMyScheduleAsync(ctx.UserId, from, to, ct);

        var data = new JsonObject
        {
            ["from"] = from.ToString("yyyy-MM-dd"),
            ["to"] = to.ToString("yyyy-MM-dd"),
            ["count"] = shifts.Count,
            ["shifts"] = new JsonArray(shifts.OrderBy(s => s.Date).ThenBy(s => s.StartTime).Select(s => (JsonNode)new JsonObject
            {
                ["date"] = s.Date.ToString("yyyy-MM-dd"),
                ["weekday"] = Vi.Weekday(s.Date),
                ["shift"] = s.ShiftName,
                ["part"] = ShiftPartClassifier.Label(ShiftPartClassifier.Classify(s.StartTime)),
                ["start"] = Vi.Time(s.StartTime),
                ["end"] = Vi.Time(s.EndTime),
                ["status"] = s.Status
            }).ToArray())
        };
        if (shifts.Count == 0)
            data["note"] = "Không có ca nào đã công bố trong khoảng này (có thể quản lý chưa công bố lịch).";
        return new ToolResult(data);
    }
}

/// <summary>Bảng công tháng của chính người hỏi.</summary>
public sealed class GetMyTimesheetTool : IAssistantTool
{
    private readonly IHrDataService _hr;

    public GetMyTimesheetTool(IHrDataService hr) => _hr = hr;

    public string Name => "get_my_timesheet";

    public string Description =>
        "Xem tổng hợp công trong tháng của CHÍNH người đang hỏi: số công thực tế, số công chuẩn, phút đi muộn/về sớm, giờ OT, ngày nghỉ. Dùng cho: 'tháng này em được bao nhiêu công'.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["month"] = Schema.Int("Tháng 1-12. Bỏ trống = tháng hiện tại."),
        ["year"] = Schema.Int("Năm, VD 2026. Bỏ trống = năm hiện tại.")
    });

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var month = ToolArgs.Int(args, "month") ?? ctx.Today.Month;
        var year = ToolArgs.Int(args, "year") ?? ctx.Today.Year;
        if (month is < 1 or > 12) throw new ToolArgumentException("month phải từ 1 đến 12.");
        if (year is < 2000 or > 2100) throw new ToolArgumentException("year không hợp lệ.");

        var t = await _hr.GetMyTimesheetAsync(ctx.UserId, year, month, ct);
        var final = t.Status is "locked" or "finalized";
        var data = new JsonObject
        {
            ["month"] = t.Month,
            ["year"] = t.Year,
            ["actual_work_days"] = t.ActualWorkDays,
            ["standard_work_days"] = t.StandardWorkDays,
            ["total_late_minutes"] = t.TotalLateMinutes,
            ["total_early_leave_minutes"] = t.TotalEarlyMinutes,
            ["overtime_hours"] = t.OvertimeHours,
            ["paid_leave_days"] = t.PaidLeaveDays,
            ["unpaid_leave_days"] = t.UnpaidLeaveDays,
            ["status"] = t.Status,
            ["note"] = final
                ? "Bảng công đã chốt."
                : (year == ctx.Today.Year && month == ctx.Today.Month
                    ? "Số liệu tạm tính tới hôm nay, chưa chốt — có thể thay đổi đến cuối tháng."
                    : "Số liệu tạm tính, chưa chốt.")
        };
        return new ToolResult(data);
    }
}

/// <summary>Quỹ phép của chính người hỏi.</summary>
public sealed class GetMyLeaveBalanceTool : IAssistantTool
{
    private readonly IHrDataService _hr;

    public GetMyLeaveBalanceTool(IHrDataService hr) => _hr = hr;

    public string Name => "get_my_leave_balance";

    public string Description => "Xem quỹ ngày phép năm của CHÍNH người đang hỏi: tổng, đã dùng, còn lại, đang chờ duyệt. Dùng cho: 'em còn mấy ngày phép'.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["year"] = Schema.Int("Năm, VD 2026. Bỏ trống = năm hiện tại.")
    });

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var year = ToolArgs.Int(args, "year") ?? ctx.Today.Year;
        var b = await _hr.GetMyLeaveBalanceAsync(ctx.UserId, year, ct);
        if (b is null)
            return new ToolResult(new JsonObject { ["year"] = year, ["found"] = false, ["note"] = "Chưa có quỹ phép cho năm này." });

        return new ToolResult(new JsonObject
        {
            ["year"] = b.Year,
            ["found"] = true,
            ["total_days"] = b.TotalDays,
            ["used_days"] = b.UsedDays,
            ["remaining_days"] = b.RemainingDays,
            ["pending_days"] = b.PendingDays,
            ["available_after_pending"] = b.RemainingDays - b.PendingDays
        });
    }
}

/// <summary>Đơn từ của chính người hỏi.</summary>
public sealed class GetMyRequestsTool : IAssistantTool
{
    private static readonly string[] Statuses = { "pending", "approved", "rejected", "cancelled" };
    private readonly IHrDataService _hr;

    public GetMyRequestsTool(IHrDataService hr) => _hr = hr;

    public string Name => "get_my_requests";

    public string Description => "Xem các đơn (nghỉ phép, đổi ca...) của CHÍNH người đang hỏi và trạng thái duyệt. Dùng cho: 'đơn nghỉ của em được duyệt chưa'.";

    public JsonObject ParametersSchema => Schema.Object(new JsonObject
    {
        ["status"] = Schema.Str("Lọc theo trạng thái. Bỏ trống = tất cả.", Statuses),
        ["limit"] = Schema.Int("Số đơn tối đa, mặc định 10, tối đa 30.")
    });

    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var status = ToolArgs.Str(args, "status")?.ToLowerInvariant();
        if (status is not null && !Statuses.Contains(status))
            throw new ToolArgumentException($"status chỉ nhận: {string.Join(" | ", Statuses)}.");
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 30);

        var items = await _hr.GetMyRequestsAsync(ctx.UserId, status, limit, ct);
        return new ToolResult(new JsonObject
        {
            ["count"] = items.Count,
            ["requests"] = new JsonArray(items.Select(r => (JsonNode)new JsonObject
            {
                ["id"] = r.Id,
                ["type"] = r.LeaveTypeName,
                ["start_date"] = r.StartDate.ToString("yyyy-MM-dd"),
                ["end_date"] = r.EndDate.ToString("yyyy-MM-dd"),
                ["days"] = r.DurationDays,
                ["reason"] = r.Reason,
                ["status"] = r.Status,
                ["approver_note"] = r.ApproverNote,
                ["created_at"] = r.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            }).ToArray())
        });
    }
}
