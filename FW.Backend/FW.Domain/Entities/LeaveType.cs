using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Loại đơn từ (bảng leave_types) do HR/Admin cấu hình (UC 1.4). Nghiệp vụ của đơn dựa vào <see cref="Category"/>.
/// </summary>
public class LeaveType : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Giá trị thuộc <see cref="RequestCategory"/>.</summary>
    public string Category { get; set; } = RequestCategory.Leave;

    public string? Description { get; set; }

    /// <summary>Ngày nghỉ được hưởng lương (dùng khi tổng hợp bảng công).</summary>
    public bool IsPaid { get; set; } = true;

    /// <summary>Trừ quỹ phép năm khi được duyệt — chỉ áp dụng cho nhóm nghỉ.</summary>
    public bool DeductsBalance { get; set; }

    /// <summary>Bắt buộc gửi kèm ảnh minh chứng.</summary>
    public bool RequiresAttachment { get; set; }

    /// <summary>false = ngừng nhận đơn mới loại này (đơn cũ vẫn giữ).</summary>
    public bool IsActive { get; set; } = true;
}
