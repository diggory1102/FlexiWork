using System.ComponentModel.DataAnnotations;
using FW.Domain.Entities;

namespace FW.Application.Features.Organization;

// =============================================================================
// Chi nhánh
// =============================================================================

public record BranchDto(
    int Id,
    string BranchCode,
    string Name,
    string Address,
    string? Phone,
    string? Email,
    decimal Latitude,
    decimal Longitude,
    int Radius,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static BranchDto From(Branch b) => new(
        b.Id, b.BranchCode, b.Name, b.Address, b.Phone, b.Email,
        b.Latitude, b.Longitude, b.Radius, b.Status, b.CreatedAt, b.UpdatedAt);
}

/// <summary>Chi tiết chi nhánh kèm danh sách Wi-Fi nội bộ.</summary>
public record BranchDetailDto(BranchDto Branch, IReadOnlyList<BranchWifiDto> Wifis);

public class BranchRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã chi nhánh.")]
    [StringLength(20, ErrorMessage = "Mã chi nhánh tối đa 20 ký tự.")]
    public string BranchCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên chi nhánh.")]
    [StringLength(150, ErrorMessage = "Tên chi nhánh tối đa 150 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự.")]
    public string Address { get; set; } = string.Empty;

    [StringLength(15, ErrorMessage = "Số điện thoại tối đa 15 ký tự.")]
    [RegularExpression(@"^[0-9+ ]*$", ErrorMessage = "Số điện thoại chỉ gồm chữ số, dấu + và khoảng trắng.")]
    public string? Phone { get; set; }

    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string? Email { get; set; }

    [Range(-90, 90, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90.")]
    public decimal Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180.")]
    public decimal Longitude { get; set; }

    [Range(10, 5000, ErrorMessage = "Bán kính Geofence phải từ 10 đến 5000 mét.")]
    public int Radius { get; set; } = 50;

    /// <summary>active / inactive. Bỏ trống khi tạo mới = active.</summary>
    public string? Status { get; set; }
}

// =============================================================================
// Wi-Fi chi nhánh
// =============================================================================

public record BranchWifiDto(int Id, int BranchId, string Ssid, string Bssid, bool IsActive, DateTime CreatedAt)
{
    public static BranchWifiDto From(BranchWifi w) => new(w.Id, w.BranchId, w.Ssid, w.Bssid, w.IsActive, w.CreatedAt);
}

public class BranchWifiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên mạng (SSID).")]
    [StringLength(100, ErrorMessage = "SSID tối đa 100 ký tự.")]
    public string Ssid { get; set; } = string.Empty;

    /// <summary>Địa chỉ MAC của router, chấp nhận dạng AA:BB:CC:DD:EE:FF hoặc aa-bb-cc-dd-ee-ff.</summary>
    [Required(ErrorMessage = "Vui lòng nhập BSSID.")]
    public string Bssid { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

// =============================================================================
// Phòng ban
// =============================================================================

public record DepartmentDto(int Id, string Code, string Name, string? Description, DateTime CreatedAt, DateTime? UpdatedAt)
{
    public static DepartmentDto From(Department d) => new(d.Id, d.Code, d.Name, d.Description, d.CreatedAt, d.UpdatedAt);
}

public class DepartmentRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã phòng ban.")]
    [StringLength(20, ErrorMessage = "Mã phòng ban tối đa 20 ký tự.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên phòng ban.")]
    [StringLength(100, ErrorMessage = "Tên phòng ban tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự.")]
    public string? Description { get; set; }
}

// =============================================================================
// Chức vụ & đơn giá công
// =============================================================================

public record PositionDto(int Id, string Title, decimal BaseSalaryPerHour, string? Description, DateTime CreatedAt, DateTime? UpdatedAt)
{
    public static PositionDto From(Position p) => new(p.Id, p.Title, p.BaseSalaryPerHour, p.Description, p.CreatedAt, p.UpdatedAt);
}

public class PositionRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên chức danh.")]
    [StringLength(100, ErrorMessage = "Tên chức danh tối đa 100 ký tự.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Đơn giá công theo giờ (VNĐ) — cột DECIMAL(12,2).</summary>
    [Range(typeof(decimal), "0", "9999999999.99", ErrorMessage = "Đơn giá công theo giờ phải từ 0 đến 9.999.999.999,99.")]
    public decimal BaseSalaryPerHour { get; set; }

    [StringLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự.")]
    public string? Description { get; set; }
}
