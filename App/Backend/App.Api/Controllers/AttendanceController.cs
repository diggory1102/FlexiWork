using App.Application.DTOs.Attendance;
using App.Application.Interfaces.Repositories;
using App.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IAttendanceRepository _attendanceRepository;

        public AttendanceController(
            IAttendanceService attendanceService,
            IAttendanceRepository attendanceRepository)
        {
            _attendanceService = attendanceService;
            _attendanceRepository = attendanceRepository;
        }

        /// <summary>
        /// UC 4.1 & UC 4.2: Thực hiện Check-in đối soát Đa lớp (Face ID + Geofencing + Wi-Fi)
        /// </summary>
        [HttpPost("checkin")]
        public async Task<IActionResult> Checkin(
            [FromForm] int userId,
            [FromForm] int branchId,
            [FromForm] decimal latitude,
            [FromForm] decimal longitude,
            [FromForm] string? bssid,
            [FromForm] long? scheduleId,
            IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chụp ảnh khuôn mặt (Selfie) để xác thực chấm công." });
            }

            using var memoryStream = new MemoryStream();
            await imageFile.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var requestDto = new CheckinRequestDto
            {
                UserId = userId,
                BranchId = branchId,
                ScheduleId = scheduleId,
                Latitude = latitude,
                Longitude = longitude,
                Bssid = bssid,
                ImageStream = memoryStream,
                ImageFileName = imageFile.FileName
            };

            var result = await _attendanceService.ProcessCheckinAsync(requestDto);
            return Ok(result);
        }

        /// <summary>
        /// UC 4.1: Thực hiện Check-out kết thúc ca làm việc
        /// </summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(
            [FromForm] long attendanceId,
            [FromForm] decimal latitude,
            [FromForm] decimal longitude,
            [FromForm] string? bssid,
            [FromForm] string? note,
            IFormFile? imageFile)
        {
            MemoryStream? memoryStream = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                memoryStream = new MemoryStream();
                await imageFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
            }

            var requestDto = new CheckoutRequestDto
            {
                AttendanceId = attendanceId,
                Latitude = latitude,
                Longitude = longitude,
                Bssid = bssid,
                Note = note,
                ImageStream = memoryStream,
                ImageFileName = imageFile?.FileName
            };

            var result = await _attendanceService.ProcessCheckoutAsync(requestDto);
            return Ok(result);
        }

        /// <summary>
        /// UC 4.3: Xem lượt điểm danh hôm nay của nhân viên
        /// </summary>
        [HttpGet("today/{userId:int}")]
        public async Task<IActionResult> GetTodayAttendance(int userId)
        {
            var log = await _attendanceRepository.GetTodayAttendanceAsync(userId, DateTime.Today);
            if (log == null)
            {
                return NotFound(new { message = "Hôm nay chưa có lượt chấm công nào." });
            }

            return Ok(log);
        }

        /// <summary>
        /// UC 4.5: Lấy nhật ký chấm công theo chi nhánh
        /// </summary>
        [HttpGet("branch/{branchId:int}")]
        public async Task<IActionResult> GetBranchAttendance(int branchId, [FromQuery] DateTime? date)
        {
            var queryDate = date ?? DateTime.Today;
            var logs = await _attendanceRepository.GetBranchAttendanceByDateAsync(branchId, queryDate);
            return Ok(logs);
        }
    }
}
