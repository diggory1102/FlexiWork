using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Một bậc trong quy tắc xử lý vi phạm giờ giấc (bảng violation_rules), áp dụng toàn công ty.
/// Bậc được chọn là bậc có <see cref="MinMinutes"/> lớn nhất không vượt quá số phút vi phạm
/// (chỉ áp dụng MỘT bậc, không cộng dồn) — xem <see cref="Services.ViolationPolicy"/>.
/// </summary>
public class ViolationRule : BaseEntity, IAuditableEntity
{
    /// <summary>Giá trị thuộc <see cref="Enums.ViolationType"/>.</summary>
    public string ViolationType { get; set; } = Enums.ViolationType.Late;

    /// <summary>Áp dụng khi số phút vi phạm từ mức này trở lên.</summary>
    public int MinMinutes { get; set; }

    /// <summary>Số công bị trừ (VD 0.5 = nửa công).</summary>
    public decimal DeductWorkUnits { get; set; }

    /// <summary>Số tiền phạt (VNĐ).</summary>
    public decimal PenaltyAmount { get; set; }

    public string? Description { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
