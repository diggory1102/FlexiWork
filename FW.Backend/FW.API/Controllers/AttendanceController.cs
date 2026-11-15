using FW.Application.Common.Exceptions;
using FW.Application.Features.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Chấm công 3 lớp trên điện thoại (UC 4.1, 4.2): GPS Geofence + Wi-Fi BSSID + Face ID.
/// Mọi vai trò có lịch làm việc đều chấm công được.
/// </summary>
[Authorize]
public class AttendanceController : ApiControllerBase
{
    private const long MaxRequestBytes = AttendanceService.MaxImageBytes + 64 * 1024;

    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
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

    private static IFormFile RequirePhoto(IFormFile? file) =>
        file ?? throw new BadRequestException("Vui lòng chụp ảnh khuôn mặt (trường form-data \"file\").");
}
