using System;
using System.IO;

namespace App.Application.DTOs.Attendance
{
    public class CheckinRequestDto
    {
        public int UserId { get; set; }
        public int BranchId { get; set; }
        public long? ScheduleId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Bssid { get; set; }
        public Stream ImageStream { get; set; } = null!;
        public string ImageFileName { get; set; } = "checkin.jpg";
    }

    public class CheckoutRequestDto
    {
        public long AttendanceId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Bssid { get; set; }
        public Stream? ImageStream { get; set; }
        public string? ImageFileName { get; set; }
        public string? Note { get; set; }
    }

    public class AttendanceResponseDto
    {
        public long AttendanceId { get; set; }
        public int UserId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public DateTime CheckinTime { get; set; }
        public DateTime? CheckoutTime { get; set; }
        public string Status { get; set; } = "valid";
        public float DistanceMeters { get; set; }
        public bool IsWithinGeofence { get; set; }
        public bool IsWifiMatched { get; set; }
        public bool IsFaceMatched { get; set; }
        public float FaceConfidence { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public decimal ActualWorkHours { get; set; }
        public string? Message { get; set; }
    }
}
