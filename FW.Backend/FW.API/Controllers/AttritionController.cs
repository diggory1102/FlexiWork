using FW.Application.Common.Interfaces;
using FW.Application.Features.Attrition;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Cảnh báo nguy cơ nghỉ việc (UC 10.3 – 10.4): tín hiệu THAM KHẢO cho Quản lý / HR, không dùng để kỷ luật.
/// Quản lý chỉ thấy nhân viên chi nhánh mình; Admin / HR thấy toàn công ty.
/// </summary>
[Authorize(Roles = RoleNames.Managers)]
[Route("api/attrition")]
public class AttritionController : ApiControllerBase
{
    private readonly AttritionQueryService _query;
    private readonly AttritionScoringService _scoring;
    private readonly AttritionNarrator _narrator;
    private readonly ICurrentUserService _currentUser;

    public AttritionController(
        AttritionQueryService query, AttritionScoringService scoring, AttritionNarrator narrator, ICurrentUserService currentUser)
    {
        _query = query;
        _scoring = scoring;
        _narrator = narrator;
        _currentUser = currentUser;
    }

    private AttritionCaller Caller() =>
        new(_currentUser.UserId ?? throw new UnauthorizedAccessException(), _currentUser.BranchId, _currentUser.Roles);

    /// <summary>Nhân sự cần quan tâm (lần chấm gần nhất, điểm cao → thấp): branchId, minLevel (medium | high, mặc định medium), limit.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int? branchId, [FromQuery] string? minLevel, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var level = string.Equals(minLevel, "high", StringComparison.OrdinalIgnoreCase) ? RiskLevel.High : RiskLevel.Medium;
        var items = await _query.GetAlertsAsync(Caller(), branchId, level, limit, cancellationToken);
        return Success(items.Select(AttritionAlertDto.From).ToList());
    }

    /// <summary>Số nhân viên theo mức nguy cơ (cho ô cảnh báo ở Dashboard): branchId.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int? branchId, CancellationToken cancellationToken) =>
        Success(await _query.GetSummaryAsync(Caller(), branchId, cancellationToken));

    /// <summary>Diễn biến điểm theo tuần của một nhân viên (cũ → mới): weeks (mặc định 12).</summary>
    [HttpGet("employees/{userId:int}/trend")]
    public async Task<IActionResult> GetTrend(int userId, [FromQuery] int weeks = 12, CancellationToken cancellationToken = default)
    {
        var history = await _query.GetTrendAsync(Caller(), userId, weeks, cancellationToken);
        return Success(history.Select(AttritionAlertDto.From).ToList());
    }

    /// <summary>Ghi chú tóm tắt dễ đọc về một nhân viên (do mô hình viết từ số liệu có sẵn; lỗi mô hình thì trả bản dựng sẵn).</summary>
    [HttpGet("employees/{userId:int}/narrative")]
    public async Task<IActionResult> GetNarrative(int userId, CancellationToken cancellationToken)
    {
        var history = await _query.GetTrendAsync(Caller(), userId, 1, cancellationToken);
        if (history.Count == 0) throw new FW.Application.Common.Exceptions.NotFoundException("Chưa có điểm nguy cơ của nhân viên này.");
        return Success(new AttritionNarrativeDto(userId, await _narrator.SummarizeAsync(history[^1], cancellationToken)));
    }

    /// <summary>Chấm điểm lại ngay cho tuần hiện tại (Admin / HR) — tác vụ nền vẫn tự chạy mỗi Thứ Hai 02:00.</summary>
    [Authorize(Roles = RoleNames.AdminOrHr)]
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken) =>
        Success(await _scoring.RunAsync(null, cancellationToken), "Đã chấm điểm xong.");
}
