using FW.Application.Common.Exceptions;
using FW.Application.Features.Requests;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Đơn từ (UC 5.1, 5.2, 5.6): nghỉ, đổi ca, làm thêm giờ, giải trình đi muộn / về sớm. Mọi vai trò tạo và theo dõi đơn của mình;
/// danh sách quản lý và duyệt đơn: Admin, HR (mọi chi nhánh), Quản lý (chi nhánh mình).
/// </summary>
[Authorize]
[Route("api/leave-requests")]
public class LeaveRequestsController : ApiControllerBase
{
    private const long MaxRequestBytes = LeaveRequestService.MaxImageBytes + 64 * 1024;

    private readonly ILeaveRequestService _requestService;
    private readonly ILeaveApprovalService _approvalService;

    public LeaveRequestsController(ILeaveRequestService requestService, ILeaveApprovalService approvalService)
    {
        _requestService = requestService;
        _approvalService = approvalService;
    }

    /// <summary>
    /// Tạo đơn (form-data). Chung: leaveTypeId, reason, file (ảnh minh chứng, tùy loại đơn). Nghỉ: startDate, endDate.
    /// OT: startAt, endAt. Đổi ca: sourceScheduleId + targetScheduleId (đổi ca) hoặc targetUserId (nhờ làm thay).
    /// Giải trình: attendanceLogId.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create([FromForm] CreateLeaveRequestForm form, IFormFile? file, CancellationToken cancellationToken)
    {
        LeaveRequestDto result;
        if (file is null)
        {
            result = await _requestService.CreateAsync(form, null, cancellationToken);
        }
        else
        {
            await using var stream = file.OpenReadStream();
            result = await _requestService.CreateAsync(form,
                new RequestAttachment(stream, file.FileName, file.ContentType ?? string.Empty, file.Length), cancellationToken);
        }

        var message = result.Status == RequestStatus.PendingPeer
            ? $"Đã gửi đơn — chờ {result.TargetUser?.FullName} đồng ý đổi ca, sau đó Quản lý duyệt."
            : "Đã gửi đơn, chờ Quản lý duyệt.";
        return Created(result, message);
    }

    /// <summary>Đơn của tôi: lọc trạng thái, nhóm đơn, khoảng ngày; mới nhất trước.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] LeaveRequestFilter filter, CancellationToken cancellationToken) =>
        Success(await _requestService.GetMineAsync(filter, cancellationToken));

    /// <summary>Đơn đổi ca đồng nghiệp gửi tới tôi (lọc status=pending_peer để lấy đơn đang chờ tôi trả lời).</summary>
    [HttpGet("peer")]
    public async Task<IActionResult> GetPeer([FromQuery] LeaveRequestFilter filter, CancellationToken cancellationToken) =>
        Success(await _requestService.GetPeerRequestsAsync(filter, cancellationToken));

    /// <summary>Đơn trong phạm vi quản lý: lọc chi nhánh (Admin/HR), nhân viên, trạng thái, nhóm đơn, khoảng ngày, tên / mã NV.</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetManaged([FromQuery] LeaveRequestFilter filter, CancellationToken cancellationToken) =>
        Success(await _requestService.GetManagedAsync(filter, cancellationToken));

    /// <summary>Gợi ý cho đơn đổi ca: ca của đồng nghiệp cùng chi nhánh (±7 ngày) và đồng nghiệp có thể làm thay.</summary>
    [HttpGet("swap-options")]
    public async Task<IActionResult> GetSwapOptions([FromQuery] long sourceScheduleId, CancellationToken cancellationToken) =>
        Success(await _requestService.GetSwapOptionsAsync(sourceScheduleId, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Success(await _requestService.GetByIdAsync(id, cancellationToken));

    /// <summary>Rút đơn đang chờ xử lý (chỉ người tạo).</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken) =>
        Success(await _requestService.CancelAsync(id, cancellationToken), "Đã rút đơn.");

    /// <summary>Đồng nghiệp trả lời đơn đổi ca: {accepted, note}.</summary>
    [HttpPost("{id:int}/peer-response")]
    public async Task<IActionResult> RespondAsPeer(int id, [FromBody] PeerResponseRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.RespondAsPeerAsync(id, request, cancellationToken);
        return Success(result, result.Status == RequestStatus.Pending
            ? "Đã đồng ý đổi ca — đơn được chuyển tới Quản lý duyệt."
            : "Đã từ chối đổi ca.");
    }

    /// <summary>
    /// Duyệt / từ chối đơn đang chờ duyệt (UC 5.3): {approved, note} — từ chối phải ghi lý do. Duyệt áp dụng luôn thay đổi kèm theo
    /// (trừ phép, ca chuyển sang nghỉ, đổi người làm ca, lượt chấm công hợp lệ); thông báo trả về tóm tắt các thay đổi.
    /// </summary>
    [HttpPut("{id:int}/review")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewLeaveRequest request, CancellationToken cancellationToken)
    {
        var result = await _approvalService.ReviewAsync(id, request, cancellationToken);
        return Success(result.Request, result.Message);
    }

    /// <summary>Quỹ phép năm của tôi: tổng, đã dùng, còn lại, đang chờ duyệt, còn xin được.</summary>
    [HttpGet("~/api/leave-balances/me")]
    public async Task<IActionResult> GetMyBalance([FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100) throw new BadRequestException("Năm không hợp lệ.");
        return Success(await _requestService.GetMyBalanceAsync(year, cancellationToken));
    }
}
