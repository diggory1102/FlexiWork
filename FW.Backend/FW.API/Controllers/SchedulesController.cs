using FW.Application.Features.Scheduling;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Phân ca & xếp lịch (UC 3.2 – 3.4).
/// Xem lịch cá nhân: mọi tài khoản. Xếp / công bố / hủy: Admin, HR (mọi chi nhánh), Quản lý (chi nhánh của mình).
/// </summary>
[Authorize]
public class SchedulesController : ApiControllerBase
{
    private readonly IScheduleService _scheduleService;

    public SchedulesController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    /// <summary>Lịch đã công bố của tôi (mặc định 14 ngày từ hôm nay; tối đa 62 ngày).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken) =>
        Success(await _scheduleService.GetMySchedulesAsync(fromDate, toDate, cancellationToken));

    /// <summary>Lịch chi nhánh trong khoảng ngày (kể cả lịch nháp và ca đã hủy).</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetBranch([FromQuery] ScheduleRangeQuery query, CancellationToken cancellationToken) =>
        Success(await _scheduleService.GetBranchSchedulesAsync(query, cancellationToken));

    /// <summary>Xếp nhiều ca (dạng nháp). Có xung đột thì không xếp ca nào và trả về danh sách xung đột.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Create([FromBody] CreateSchedulesRequest request, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.CreateAsync(request, cancellationToken);
        return Created(result, $"Đã xếp {result.Created} ca (bản nháp). Bấm \"Công bố\" để nhân viên thấy lịch.");
    }

    /// <summary>Sao chép lịch một khoảng ngày (VD tuần trước) sang khoảng khác, bỏ qua ca không hợp lệ / xung đột.</summary>
    [HttpPost("copy")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Copy([FromBody] CopySchedulesRequest request, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.CopyAsync(request, cancellationToken);
        var message = result.Skipped == 0
            ? $"Đã sao chép {result.Created} ca (bản nháp)."
            : $"Đã sao chép {result.Created} ca (bản nháp), bỏ qua {result.Skipped} ca.";
        return Created(result, message);
    }

    /// <summary>Công bố toàn bộ lịch nháp của chi nhánh trong khoảng ngày.</summary>
    [HttpPost("publish")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Publish([FromBody] ScheduleRangeRequest request, CancellationToken cancellationToken)
    {
        var count = await _scheduleService.PublishAsync(request, cancellationToken);
        return Success(count, count == 0 ? "Không có lịch nháp nào cần công bố." : $"Đã công bố {count} ca.");
    }

    /// <summary>Xóa toàn bộ lịch nháp của chi nhánh trong khoảng ngày.</summary>
    [HttpDelete("drafts")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> DeleteDrafts([FromQuery] ScheduleRangeRequest request, CancellationToken cancellationToken)
    {
        var count = await _scheduleService.DeleteDraftsAsync(request, cancellationToken);
        return Success(count, $"Đã xóa {count} ca nháp.");
    }

    /// <summary>Lịch nháp: xóa hẳn. Lịch đã công bố và chưa tới giờ: hủy ca (giữ lại để đối soát).</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Remove(long id, CancellationToken cancellationToken)
    {
        var deleted = await _scheduleService.RemoveAsync(id, cancellationToken);
        return Success(deleted ? "Đã xóa ca nháp." : "Đã hủy ca làm việc.");
    }
}
