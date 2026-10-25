namespace App.Domain.Entities
{
    /// <summary>
    /// Quỹ ngày phép năm của từng nhân viên
    /// </summary>
    public class LeaveBalance : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int Year { get; set; }
        public decimal TotalDays { get; set; } = 12.0m;
        public decimal UsedDays { get; set; } = 0.0m;
        public decimal RemainingDays { get; set; } = 12.0m;
    }
}
