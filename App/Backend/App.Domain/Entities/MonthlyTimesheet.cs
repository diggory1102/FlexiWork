using System;

namespace App.Domain.Entities
{
    /// <summary>
    /// Bảng tổng hợp công & tính lương tháng
    /// </summary>
    public class MonthlyTimesheet : BaseEntity<long>
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int Month { get; set; }
        public int Year { get; set; }

        public decimal StandardWorkDays { get; set; } = 26.0m;
        public decimal ActualWorkDays { get; set; } = 0.0m;
        public int TotalLateMinutes { get; set; } = 0;
        public int TotalEarlyMinutes { get; set; } = 0;
        public decimal TotalOvertimeHours { get; set; } = 0.00m;
        public decimal PaidLeaveDays { get; set; } = 0.0m;
        public decimal UnpaidLeaveDays { get; set; } = 0.0m;
        public decimal CalculatedSalary { get; set; } = 0.00m;

        public string Status { get; set; } = "draft";
        public string? Note { get; set; }
    }

    [Obsolete("Sử dụng MonthlyTimesheet thay thế cho Payroll")]
    public class Payroll : MonthlyTimesheet
    {
    }
}
