using FW.Application.Common.Exceptions;
using FW.Application.Features.Attendance;
using FW.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Chấm công 3 lớp trên điện thoại (UC 4.1, 4.2): GPS Geofence + Wi-Fi BSSID + Face ID.
/// Mọi vai trò có lịch làm việc đều chấm công được. Tra cứu & duyệt nhật ký (UC 4.5, 4.6): Admin, HR (mọi chi nhánh),
/// Quản lý (chi nhánh của mình); nhân viên xem lịch sử của chính mình.
/// </summary>
[Authorize]
public class AttendanceController : ApiControllerBase
{
    private const long MaxRequestBytes = AttendanceService.MaxImageBytes + 64 * 1024;

    private readonly IAttendanceService _attendanceService;
    private readonly IAttendanceLogService _logService;
    private readonly IAttendanceAdjustmentService _adjustmentService;

    public AttendanceController(IAttendanceService attendanceService, IAttendanceLogService logService, IAttendanceAdjustmentService adjustmentService)
    {
        _attendanceService = attendanceService;
        _logService = logService;
        _adjustmentService = adjustmentService;
    }

    /// <summary>Việc cần làm tiếp theo (check-in / check-out / không có), chi nhánh của ca và các ca trong ngày.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> Today(CancellationToken cancellationToken) =>
        Success(await _attendanceService.GetTodayAsync(cancellationToken));

    /// <summary>
    /// Check-in (form-data): "file" = ảnh selfie; latitude, longitude, accuracy, bssid, deviceId, isMocked.
    /// </summary>
    [HttpPost("check-in")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> CheckIn([FromForm] AttendanceCaptureRequest request, IFormFile? file, CancellationToken cancellationToken)
    {
        var photo = RequirePhoto(file);
        await using var stream = photo.OpenReadStream();
        var result = await _attendanceService.CheckInAsync(request, stream, photo.FileName, photo.ContentType, photo.Length, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Check-out (form-data, cùng các trường như check-in).</summary>
    [HttpPost("check-out")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> CheckOut([FromForm] AttendanceCaptureRequest request, IFormFile? file, CancellationToken cancellationToken)
    {
        var photo = RequirePhoto(file);
        await using var stream = photo.OpenReadStream();
        var result = await _attendanceService.CheckOutAsync(request, stream, photo.FileName, photo.ContentType, photo.Length, cancellationToken);
        return Success(result, result.Message);
    }

    /// <summary>Lịch sử chấm công của tôi (mặc định từ đầu tháng tới hôm nay; tối đa 366 ngày; phân trang).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] AttendanceLogFilter filter, CancellationToken cancellationToken) =>
        Success(await _logService.GetMineAsync(filter, cancellationToken));

    /// <summary>
    /// Tra cứu nhật ký chấm công: lọc chi nhánh, nhân viên, khoảng ngày công (mặc định 7 ngày gần nhất), trạng thái, từ khóa
    /// (tên / mã nhân viên); phân trang.
    /// </summary>
    [HttpGet("logs")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> GetLogs([FromQuery] AttendanceLogFilter filter, CancellationToken cancellationToken) =>
        Success(await _logService.GetLogsAsync(filter, cancellationToken));

    /// <summary>Chi tiết minh chứng của một lượt chấm công (chính chủ hoặc người quản lý chi nhánh).</summary>
    [HttpGet("logs/{id:long}")]
    public async Task<IActionResult> GetLog(long id, CancellationToken cancellationToken) =>
        Success(await _logService.GetByIdAsync(id, cancellationToken));

    /// <summary>Quản lý / HR nhập giờ ra thay nhân viên quên check-out: {checkoutTime, note}. Tính lại giờ làm, báo cho nhân viên; bị chặn khi bảng công đã khóa.</summary>
    [HttpPut("logs/{id:long}/checkout")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> AdjustCheckout(long id, [FromBody] AdjustCheckoutRequest request, CancellationToken cancellationToken) =>
        Success(await _adjustmentService.AdjustCheckoutAsync(id, request, cancellationToken), "Đã ghi giờ ra cho nhân viên.");

    /// <summary>Chấp nhận / từ chối lượt chấm công đang chờ duyệt (từ chối phải ghi lý do).</summary>
    [HttpPut("logs/{id:long}/review")]
    [Authorize(Roles = RoleNames.Managers)]
    public async Task<IActionResult> Review(long id, [FromBody] ReviewAttendanceRequest request, CancellationToken cancellationToken)
    {
        var result = await _logService.ReviewAsync(id, request, cancellationToken);
        return Success(result, result.Log.Status == AttendanceStatus.Valid
            ? "Đã chấp nhận lượt chấm công."
            : "Đã từ chối lượt chấm công — lượt này không được tính công.");
    }

    private static IFormFile RequirePhoto(IFormFile? file) =>
        file ?? throw new BadRequestException("Vui lòng chụp ảnh khuôn mặt (trường form-data \"file\").");
}
