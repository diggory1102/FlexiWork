using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Chi nhánh / cửa hàng kèm tâm tọa độ và bán kính Geofence hợp lệ (bảng branches).
/// </summary>
public class Branch : BaseAuditableEntity
{
    public string BranchCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Vĩ độ tâm chi nhánh.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Kinh độ tâm chi nhánh.</summary>
    public decimal Longitude { get; set; }

    /// <summary>Bán kính Geofence hợp lệ (mét).</summary>
    public int Radius { get; set; } = 50;

    /// <summary>Giá trị thuộc <see cref="BranchStatus"/>.</summary>
    public string Status { get; set; } = BranchStatus.Active;
}
