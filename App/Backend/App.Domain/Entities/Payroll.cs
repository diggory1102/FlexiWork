using App.Domain.Enums;
namespace App.Domain.Entities
{
    public class Payroll : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal StandardWorkDays { get; set; }
        public decimal ActualWorkDays { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal TotalAllowances { get; set; }
        public decimal TotalPenalties { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }
        public PayrollStatus Status { get; set; }
    }
}
