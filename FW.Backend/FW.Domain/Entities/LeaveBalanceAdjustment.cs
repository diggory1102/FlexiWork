using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>Một lần HR cộng / trừ quỹ phép năm của nhân viên kèm lý do (bảng leave_balance_adjustments, UC 5.5).</summary>
public class LeaveBalanceAdjustment : BaseEntity
{
    public int UserId { get; set; }
    public int Year { get; set; }

    /// <summary>Số ngày cộng thêm (dương) hoặc trừ bớt (âm) vào tổng quỹ phép.</summary>
    public decimal DeltaDays { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Người điều chỉnh; null nếu tài khoản đó đã bị xóa.</summary>
    public int? CreatedBy { get; set; }

    /// <summary>Do CSDL tự gán.</summary>
    public DateTime CreatedAt { get; set; }
}
