using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Điểm truy cập Wi-Fi nội bộ hợp lệ để chấm công tại chi nhánh (bảng branch_wifis).
/// </summary>
public class BranchWifi : BaseAuditableEntity
{
    public int BranchId { get; set; }

    /// <summary>Tên mạng hiển thị.</summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>Địa chỉ MAC của router, định dạng XX:XX:XX:XX:XX:XX (viết hoa).</summary>
    public string Bssid { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
