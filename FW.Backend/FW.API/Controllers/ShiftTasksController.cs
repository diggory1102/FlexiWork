using FW.Application.Features.ShiftTasks;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Checklist công việc theo ca (UC 6.1 – 6.4). Mẫu checklist: xem — Admin, HR, Quản lý; thêm / sửa / xóa — Admin (mọi việc),
/// Quản lý (việc riêng chi nhánh mình). Checklist trong ca: nhân viên xem, tích / bỏ tích việc của mình khi đang trong ca;
/// Quản lý / HR / Admin xem checklist và tiến độ theo phạm vi chi nhánh.
/// </summary>
[Authorize]
[Route("api/shift-tasks")]
public class ShiftTasksController : ApiControllerBase
{
    private const string AdminOrManager = RoleNames.Admin + "," + RoleNames.Manager;
    private const long MaxRequestBytes = ShiftTaskService.MaxImageBytes + 64 * 1024;

    private readonly IShiftTaskService _taskService;

    public ShiftTasksController(IShiftTaskService taskService)
    {
        _taskService = taskService;
    }

    /// <summary>Mẫu checklist: lọc theo ca, chi nhánh (Admin/HR; Quản lý luôn là chi nhánh mình).</summary>
    [HttpGet("templates")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetTemplates([FromQuery] ShiftTaskTemplateFilter filter, CancellationToken cancellationToken) =>
        Success(await _taskService.GetTemplatesAsync(filter, cancellationToken));

    [HttpPost("templates")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<IActionResult> CreateTemplate([FromBody] ShiftTaskTemplateRequest request, CancellationToken cancellationToken) =>
        Created(await _taskService.CreateTemplateAsync(request, cancellationToken), "Đã thêm việc vào checklist.");

    [HttpPut("templates/{id:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] ShiftTaskTemplateRequest request, CancellationToken cancellationToken) =>
        Success(await _taskService.UpdateTemplateAsync(id, request, cancellationToken), "Đã cập nhật việc trong checklist.");

    [HttpDelete("templates/{id:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken cancellationToken)
    {
        await _taskService.DeleteTemplateAsync(id, cancellationToken);
        return Success("Đã xóa việc khỏi checklist (các ca đã giao vẫn giữ lịch sử).");
    }

    /// <summary>Checklist các ca của tôi trong ngày (mặc định hôm nay).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        Success(await _taskService.GetMyTasksAsync(date, cancellationToken));

    /// <summary>Checklist của một ca (người làm ca hoặc người quản lý chi nhánh).</summary>
    [HttpGet("schedules/{scheduleId:long}")]
    public async Task<IActionResult> GetSchedule(long scheduleId, CancellationToken cancellationToken) =>
        Success(await _taskService.GetScheduleTasksAsync(scheduleId, cancellationToken));

    /// <summary>Tích hoàn thành (form-data): note, file (ảnh hiện trường — bắt buộc với việc cần ảnh).</summary>
    [HttpPost("assignments/{id:long}/complete")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Complete(long id, [FromForm] CompleteTaskForm form, IFormFile? file, CancellationToken cancellationToken)
    {
        ScheduleTasksDto result;
        if (file is null)
        {
            result = await _taskService.CompleteAsync(id, form, null, cancellationToken);
        }
        else
        {
            await using var stream = file.OpenReadStream();
            result = await _taskService.CompleteAsync(id, form,
                new TaskPhoto(stream, file.FileName, file.ContentType ?? string.Empty, file.Length), cancellationToken);
        }
        return Success(result, $"Đã hoàn thành {result.Done}/{result.Total} việc.");
    }

    /// <summary>Bỏ tích (làm lại) khi ca còn đang diễn ra.</summary>
    [HttpPost("assignments/{id:long}/uncomplete")]
    public async Task<IActionResult> Uncomplete(long id, CancellationToken cancellationToken) =>
        Success(await _taskService.UncompleteAsync(id, cancellationToken), "Đã bỏ tích.");

    /// <summary>Tiến độ checklist các ca trong ngày: lọc ngày (mặc định hôm nay), chi nhánh (Admin/HR).</summary>
    [HttpGet("progress")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetProgress([FromQuery] TaskProgressFilter filter, CancellationToken cancellationToken) =>
        Success(await _taskService.GetProgressAsync(filter, cancellationToken));
}
