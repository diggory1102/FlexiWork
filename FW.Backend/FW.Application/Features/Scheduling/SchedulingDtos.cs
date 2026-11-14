using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FW.Domain.Entities;

namespace FW.Application.Features.Scheduling;

// =============================================================================
// Khung ca
// =============================================================================

/// <param name="StartTime">Giờ bắt đầu dạng "HH:mm".</param>
/// <param name="WorkingMinutes">Số phút làm việc thực tế (đã trừ giờ nghỉ).</param>
/// <param name="CanEdit">Người đang xem có quyền sửa/xóa ca này không (Quản lý chỉ sửa ca riêng của chi nhánh mình).</param>
public record ShiftDto(
    int Id,
    string ShiftCode,
    string Name,
    int? BranchId,
    string StartTime,
    string EndTime,
    int BreakMinutes,
    int WorkingMinutes,
    decimal WorkUnits,
    bool IsNightShift,
    bool IsCrossDay,
    int LateGraceMinutes,
    int EarlyLeaveGraceMinutes,
    bool CanEdit,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static ShiftDto From(Shift s, bool canEdit) => new(
        s.Id, s.ShiftCode, s.Name, s.BranchId,
        ShiftTime.Format(s.StartTime), ShiftTime.Format(s.EndTime),
        s.BreakMinutes, s.WorkingMinutes, s.WorkUnits, s.IsNightShift, s.IsCrossDay,
        s.LateGraceMinutes, s.EarlyLeaveGraceMinutes, canEdit, s.CreatedAt, s.UpdatedAt);
}

/// <summary>Chi tiết ca kèm cờ "đã được xếp lịch" — khi true, giao diện khóa các trường giờ giấc.</summary>
public record ShiftDetailDto(ShiftDto Shift, bool IsInUse);

public class ShiftRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã ca.")]
    [RegularExpression(@"^\s*[A-Za-z0-9_-]{2,20}\s*$", ErrorMessage = "Mã ca gồm 2–20 ký tự chữ, số, dấu '-' hoặc '_'.")]
    public string ShiftCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên ca.")]
    [StringLength(100, ErrorMessage = "Tên ca tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Chi nhánh sở hữu ca; null = ca dùng chung (chỉ Admin/HR). Quản lý bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    /// <summary>Giờ bắt đầu "HH:mm".</summary>
    [Required(ErrorMessage = "Vui lòng nhập giờ bắt đầu.")]
    public string StartTime { get; set; } = string.Empty;

    /// <summary>Giờ kết thúc "HH:mm"; nhỏ hơn giờ bắt đầu = ca qua đêm.</summary>
    [Required(ErrorMessage = "Vui lòng nhập giờ kết thúc.")]
    public string EndTime { get; set; } = string.Empty;

    [Range(0, 480, ErrorMessage = "Giờ nghỉ giữa ca từ 0 đến 480 phút.")]
    public int BreakMinutes { get; set; }

    [Range(0.1, 3.0, ErrorMessage = "Hệ số công từ 0,1 đến 3.")]
    public decimal WorkUnits { get; set; } = 1.0m;

    public bool IsNightShift { get; set; }

    [Range(0, 60, ErrorMessage = "Số phút cho phép đi muộn từ 0 đến 60.")]
    public int LateGraceMinutes { get; set; } = 5;

    [Range(0, 60, ErrorMessage = "Số phút cho phép về sớm từ 0 đến 60.")]
    public int EarlyLeaveGraceMinutes { get; set; } = 5;
}

/// <summary>Đọc / ghi giờ trong ngày dạng "HH:mm".</summary>
public static class ShiftTime
{
    private static readonly string[] Formats = { @"h\:mm", @"hh\:mm" };

    public static string Format(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static bool TryParse(string? input, out TimeSpan time)
    {
        time = default;
        return !string.IsNullOrWhiteSpace(input)
               && TimeSpan.TryParseExact(input.Trim(), Formats, CultureInfo.InvariantCulture, out time)
               && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
    }
}

// =============================================================================
// Quy tắc vi phạm & số công chuẩn
// =============================================================================

public record ViolationRuleDto(int Id, string ViolationType, int MinMinutes, decimal DeductWorkUnits, decimal PenaltyAmount, string? Description)
{
    public static ViolationRuleDto From(ViolationRule r) =>
        new(r.Id, r.ViolationType, r.MinMinutes, r.DeductWorkUnits, r.PenaltyAmount, r.Description);
}

/// <param name="StandardWorkDays">Số công chuẩn trong tháng.</param>
/// <param name="Rules">Các bậc xử lý, sắp theo loại vi phạm rồi số phút tăng dần.</param>
public record AttendancePolicyDto(decimal StandardWorkDays, IReadOnlyList<ViolationRuleDto> Rules);

public class AttendancePolicyRequest
{
    [Range(1, 31, ErrorMessage = "Số công chuẩn trong tháng từ 1 đến 31.")]
    public decimal StandardWorkDays { get; set; } = 26;

    /// <summary>Toàn bộ các bậc xử lý (thay thế bộ cũ). Danh sách rỗng = không phạt.</summary>
    [Required(ErrorMessage = "Thiếu danh sách quy tắc vi phạm.")]
    public List<ViolationRuleRequest> Rules { get; set; } = new();
}

public class ViolationRuleRequest
{
    /// <summary>late / early_leave.</summary>
    [Required(ErrorMessage = "Vui lòng chọn loại vi phạm.")]
    public string ViolationType { get; set; } = string.Empty;

    [Range(1, 600, ErrorMessage = "Số phút áp dụng từ 1 đến 600.")]
    public int MinMinutes { get; set; }

    [Range(0, 3, ErrorMessage = "Số công bị trừ từ 0 đến 3.")]
    public decimal DeductWorkUnits { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Tiền phạt từ 0 đến 10.000.000 đ.")]
    public decimal PenaltyAmount { get; set; }

    [StringLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự.")]
    public string? Description { get; set; }
}
