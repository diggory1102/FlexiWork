using System;

namespace App.Domain.Entities
{
    /// <summary>
    /// Nhật ký đối soát chấm công đa lớp: Face ID + GPS + Wi-Fi (Check-in & Check-out toàn diện)
    /// </summary>
    public class AttendanceLog : BaseEntity<long>
    {
        public long? ScheduleId { get; set; }
        public Schedule? Schedule { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // Check-in
        public DateTime CheckinTime { get; set; }
        public string CheckinImageUrl { get; set; } = string.Empty;
        public decimal CheckinLat { get; set; }
        public decimal CheckinLng { get; set; }
        public float CheckinDistance { get; set; }
        public string? CheckinBssid { get; set; }
        public bool IsFaceMatched { get; set; }
        public float? FaceMatchConfidence { get; set; }

        // Check-out
        public DateTime? CheckoutTime { get; set; }
        public string? CheckoutImageUrl { get; set; }
        public decimal? CheckoutLat { get; set; }
        public decimal? CheckoutLng { get; set; }
        public float? CheckoutDistance { get; set; }
        public string? CheckoutBssid { get; set; }

        // Đối soát ca
        public int LateMinutes { get; set; } = 0;
        public int EarlyMinutes { get; set; } = 0;
        public decimal ActualWorkHours { get; set; } = 0.00m;
        public string Status { get; set; } = "valid";
        public string? Note { get; set; }
    }
}
