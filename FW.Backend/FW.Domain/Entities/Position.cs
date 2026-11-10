using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Chức danh chuyên môn và đơn giá công theo giờ (bảng positions).
/// </summary>
public class Position : BaseAuditableEntity
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Đơn giá công theo giờ (VNĐ).</summary>
    public decimal BaseSalaryPerHour { get; set; }

    public string? Description { get; set; }
}
