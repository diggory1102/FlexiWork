using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>Quỹ ngày phép năm của một nhân viên (bảng leave_balances).</summary>
public class LeaveBalance : BaseEntity
{
    public int UserId { get; set; }
    public int Year { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays { get; set; }

    /// <summary>Do CSDL tự gán.</summary>
    public DateTime UpdatedAt { get; set; }
}
