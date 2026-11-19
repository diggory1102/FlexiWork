using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Ngoại lệ định biên của một ca tại một chi nhánh (bảng shift_staffing_rules): ghi đè số người tối thiểu / tối đa mặc định của ca
/// (<see cref="Shift.MinStaff"/> / <see cref="Shift.MaxStaff"/>) cho một ngày cụ thể hoặc cho một thứ trong tuần.
/// Mỗi (chi nhánh, ca, phạm vi) chỉ có một quy tắc; quy tắc theo ngày cụ thể thắng quy tắc theo thứ — xem <c>ShiftStaffing.Resolve</c>.
/// </summary>
public class ShiftStaffingRule : BaseEntity, IAuditableEntity
{
    public int BranchId { get; set; }
    public int ShiftId { get; set; }

    /// <summary>"date:yyyy-MM-dd" (một ngày cụ thể) hoặc "dow:1".."dow:7" (mọi thứ Hai … Chủ nhật hằng tuần).</summary>
    public string ScopeKey { get; set; } = string.Empty;

    /// <summary>Thay cho <see cref="Shift.MinStaff"/> trong phạm vi này; null = không yêu cầu tối thiểu.</summary>
    public int? MinStaff { get; set; }

    /// <summary>Thay cho <see cref="Shift.MaxStaff"/> trong phạm vi này; null = không giới hạn (gỡ giới hạn mặc định).</summary>
    public int? MaxStaff { get; set; }

    /// <summary>Lý do (VD "Tiệc đặt bàn công ty ABC").</summary>
    public string? Note { get; set; }

    /// <summary>Người tạo / sửa gần nhất; null nếu tài khoản đó đã bị xóa.</summary>
    public int? CreatedBy { get; set; }

    /// <summary>Do CSDL tự gán.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
