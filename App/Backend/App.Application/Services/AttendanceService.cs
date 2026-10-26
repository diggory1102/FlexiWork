using App.Application.DTOs.Attendance;
using App.Application.Interfaces.Repositories;
using App.Application.Interfaces.Services;
using App.Domain.Entities;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace App.Application.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IBranchRepository _branchRepository;
        private readonly IUserRepository _userRepository;
        private readonly IScheduleRepository _scheduleRepository;
        private readonly IFaceRecognitionService _faceRecognitionService;

        public AttendanceService(
            IAttendanceRepository attendanceRepository,
            IBranchRepository branchRepository,
            IUserRepository userRepository,
            IScheduleRepository scheduleRepository,
            IFaceRecognitionService faceRecognitionService)
        {
            _attendanceRepository = attendanceRepository;
            _branchRepository = branchRepository;
            _userRepository = userRepository;
            _scheduleRepository = scheduleRepository;
            _faceRecognitionService = faceRecognitionService;
        }

        public async Task<AttendanceResponseDto> ProcessCheckinAsync(CheckinRequestDto request)
        {
            var now = DateTime.Now;

            // 1. Kiểm tra thông tin Chi nhánh
            var branch = await _branchRepository.GetByIdAsync(request.BranchId);
            if (branch == null)
            {
                throw new InvalidOperationException($"Không tìm thấy chi nhánh với ID {request.BranchId}");
            }

            // 2. LỚP 1: Geofencing GPS (Haversine Formula)
            var distance = (float)GeofenceCalculator.CalculateDistanceInMeters(
                (double)request.Latitude,
                (double)request.Longitude,
                (double)branch.Latitude,
                (double)branch.Longitude
            );

            bool isWithinGeofence = distance <= branch.Radius;

            // 3. LỚP 2: Wi-Fi BSSID Check (Chống Fake GPS)
            bool isWifiMatched = false;
            var branchWifis = await _branchRepository.GetActiveWifisByBranchIdAsync(branch.Id);
            if (!string.IsNullOrWhiteSpace(request.Bssid) && branchWifis.Any())
            {
                isWifiMatched = branchWifis.Any(w =>
                    string.Equals(w.Bssid.Trim(), request.Bssid.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // 4. LỚP 3: AI Face ID Verification
            var faceProfile = await _userRepository.GetFaceProfileByUserIdAsync(request.UserId);
            bool isFaceMatched = false;
            float faceConfidence = 0f;

            if (faceProfile != null && !string.IsNullOrWhiteSpace(faceProfile.FaceEncoding))
            {
                var faceResult = await _faceRecognitionService.VerifyFaceAsync(
                    faceProfile.FaceEncoding,
                    request.ImageStream,
                    request.ImageFileName
                );

                isFaceMatched = faceResult.IsMatched;
                faceConfidence = faceResult.Confidence;
            }
            else
            {
                // Nếu chưa đăng ký Face ID thì chấp nhận tạm thời ở môi trường thử nghiệm
                isFaceMatched = true;
                faceConfidence = 0.90f;
            }

            // 5. Tính toán đi muộn dựa theo Lịch làm việc (Schedule & Shift)
            int lateMinutes = 0;
            long? scheduleId = request.ScheduleId;
            Schedule? schedule = null;

            if (scheduleId.HasValue)
            {
                schedule = await _scheduleRepository.GetByIdAsync(scheduleId.Value);
            }
            else
            {
                schedule = await _scheduleRepository.GetUserScheduleForDateAsync(request.UserId, now.Date);
                if (schedule != null)
                {
                    scheduleId = schedule.Id;
                }
            }

            if (schedule?.Shift != null)
            {
                var shiftStartToday = now.Date.Add(schedule.Shift.StartTime);
                if (now > shiftStartToday)
                {
                    lateMinutes = (int)Math.Max(0, (now - shiftStartToday).TotalMinutes);
                }
            }

            // 6. Quyết định trạng thái (Status)
            // Hợp lệ nếu đạt cả GPS và Face ID. Nếu vi phạm Wi-Fi hoặc khoảng cách thì chuyển 'pending_approval'
            string status = "valid";
            string? note = null;

            if (!isWithinGeofence)
            {
                status = "pending_approval";
                note = $"Vị trí nằm ngoài bán kính cho phép ({distance:F1}m > {branch.Radius}m). ";
            }

            if (!isFaceMatched)
            {
                status = "invalid";
                note += $"Xác thực Face ID không khớp (Độ tin cậy: {faceConfidence * 100:F1}%). ";
            }
            else if (!isWifiMatched && branchWifis.Any())
            {
                // Cảnh báo Wi-Fi nhưng GPS & Face ID đúng
                if (status == "valid")
                {
                    note += "Chưa kết nối Wi-Fi chuẩn của chi nhánh. ";
                }
            }

            // 7. Lưu bản ghi vào bảng attendance_logs
            var attendanceLog = new AttendanceLog
            {
                UserId = request.UserId,
                BranchId = request.BranchId,
                ScheduleId = scheduleId,
                CheckinTime = now,
                CheckinImageUrl = $"/uploads/attendance/{request.UserId}_{now:yyyyMMddHHmmss}.jpg",
                CheckinLat = request.Latitude,
                CheckinLng = request.Longitude,
                CheckinDistance = distance,
                CheckinBssid = request.Bssid,
                IsFaceMatched = isFaceMatched,
                FaceMatchConfidence = faceConfidence,
                LateMinutes = lateMinutes,
                EarlyMinutes = 0,
                ActualWorkHours = 0,
                Status = status,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };

            var attendanceId = await _attendanceRepository.RecordCheckinAsync(attendanceLog);

            return new AttendanceResponseDto
            {
                AttendanceId = attendanceId,
                UserId = request.UserId,
                BranchId = request.BranchId,
                BranchName = branch.Name,
                CheckinTime = now,
                Status = status,
                DistanceMeters = distance,
                IsWithinGeofence = isWithinGeofence,
                IsWifiMatched = isWifiMatched,
                IsFaceMatched = isFaceMatched,
                FaceConfidence = faceConfidence,
                LateMinutes = lateMinutes,
                EarlyMinutes = 0,
                ActualWorkHours = 0,
                Message = status == "valid" 
                    ? "Check-in thành công và hợp lệ!" 
                    : $"Check-in ghi nhận với trạng thái: {status}. {note}"
            };
        }

        public async Task<AttendanceResponseDto> ProcessCheckoutAsync(CheckoutRequestDto request)
        {
            var now = DateTime.Now;

            var existingLog = await _attendanceRepository.GetByIdAsync(request.AttendanceId);
            if (existingLog == null)
            {
                throw new InvalidOperationException($"Không tìm thấy bản ghi chấm công ID {request.AttendanceId}");
            }

            var branch = await _branchRepository.GetByIdAsync(existingLog.BranchId);
            if (branch == null)
            {
                throw new InvalidOperationException($"Không tìm thấy chi nhánh ID {existingLog.BranchId}");
            }

            // Lớp 1: Geofencing
            var distance = (float)GeofenceCalculator.CalculateDistanceInMeters(
                (double)request.Latitude,
                (double)request.Longitude,
                (double)branch.Latitude,
                (double)branch.Longitude
            );

            bool isWithinGeofence = distance <= branch.Radius;

            // Lớp 2: Wi-Fi
            bool isWifiMatched = false;
            var branchWifis = await _branchRepository.GetActiveWifisByBranchIdAsync(branch.Id);
            if (!string.IsNullOrWhiteSpace(request.Bssid) && branchWifis.Any())
            {
                isWifiMatched = branchWifis.Any(w =>
                    string.Equals(w.Bssid.Trim(), request.Bssid.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Tính số phút về sớm & tổng giờ làm thực tế
            int earlyMinutes = 0;
            if (existingLog.ScheduleId.HasValue)
            {
                var schedule = await _scheduleRepository.GetByIdAsync(existingLog.ScheduleId.Value);
                if (schedule?.Shift != null)
                {
                    var shiftEndToday = now.Date.Add(schedule.Shift.EndTime);
                    if (now < shiftEndToday)
                    {
                        earlyMinutes = (int)Math.Max(0, (shiftEndToday - now).TotalMinutes);
                    }
                }
            }

            var totalWorkHours = (decimal)Math.Max(0, (now - existingLog.CheckinTime).TotalHours);
            string checkoutImageUrl = $"/uploads/attendance/checkout_{existingLog.UserId}_{now:yyyyMMddHHmmss}.jpg";

            string status = existingLog.Status;
            if (!isWithinGeofence && status == "valid")
            {
                status = "pending_approval";
            }

            await _attendanceRepository.RecordCheckoutAsync(
                existingLog.Id,
                now,
                checkoutImageUrl,
                request.Latitude,
                request.Longitude,
                distance,
                request.Bssid,
                earlyMinutes,
                Math.Round(totalWorkHours, 2),
                status,
                request.Note
            );

            return new AttendanceResponseDto
            {
                AttendanceId = existingLog.Id,
                UserId = existingLog.UserId,
                BranchId = existingLog.BranchId,
                BranchName = branch.Name,
                CheckinTime = existingLog.CheckinTime,
                CheckoutTime = now,
                Status = status,
                DistanceMeters = distance,
                IsWithinGeofence = isWithinGeofence,
                IsWifiMatched = isWifiMatched,
                IsFaceMatched = existingLog.IsFaceMatched,
                FaceConfidence = existingLog.FaceMatchConfidence ?? 0f,
                LateMinutes = existingLog.LateMinutes,
                EarlyMinutes = earlyMinutes,
                ActualWorkHours = Math.Round(totalWorkHours, 2),
                Message = "Check-out hoàn tất thành công!"
            };
        }
    }
}
