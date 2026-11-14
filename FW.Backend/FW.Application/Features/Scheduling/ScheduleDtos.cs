using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;

namespace FW.Application.Features.Scheduling;

/// <summary>Một ca trong lịch kèm thông tin nhân viên và khung ca.</summary>
public record ScheduleDto(
    long Id,
    int UserId,
    string EmployeeCode,
    string FullName,
    int ShiftId,
    string ShiftCode,
    string ShiftName,
    string StartTime,
    string EndTime,
    bool IsCrossDay,
    int BranchId,
    DateOnly WorkDate,
    string Status,
    bool IsPublished,
    DateTime? PublishedAt)
{
    public static ScheduleDto From(ScheduleDetail d) => new(
        d.Id, d.UserId, d.EmployeeCode, d.FullName, d.ShiftId, d.ShiftCode, d.ShiftName,
        ShiftTime.Format(d.StartTime), ShiftTime.Format(d.EndTime), d.IsCrossDay, d.BranchId,
        DateOnly.FromDateTime(d.WorkDate), d.Status, d.IsPublished, d.PublishedAt);
}

/// <summary>Xem lịch chi nhánh (lưới tuần / tháng). Quản lý luôn bị giới hạn về chi nhánh của mình.</summary>
public class ScheduleRangeQuery
{
    /// <summary>Bắt buộc với Admin/HR; Quản lý bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Chỉ xem lịch của một nhân viên.</summary>
    public int? UserId { get; set; }
}

/// <summary>Khoảng ngày của một chi nhánh — dùng cho công bố lịch và xóa lịch nháp.</summary>
public class ScheduleRangeRequest
{
    /// <summary>Bắt buộc với Admin/HR; Quản lý bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}

public class ScheduleItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Thiếu nhân viên.")]
    public int UserId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Thiếu ca làm việc.")]
    public int ShiftId { get; set; }

    public DateOnly WorkDate { get; set; }
}

/// <summary>Xếp nhiều ca một lần (cả tuần / tháng, ca xoay do giao diện sinh sẵn). Lịch tạo ra ở dạng nháp.</summary>
public class CreateSchedulesRequest
{
    /// <summary>Bắt buộc với Admin/HR; Quản lý bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    [Required(ErrorMessage = "Thiếu danh sách ca cần xếp.")]
    [MinLength(1, ErrorMessage = "Chưa chọn ca nào để xếp.")]
    [MaxLength(ScheduleService.MaxItemsPerRequest, ErrorMessage = "Mỗi lần xếp tối đa 500 ca.")]
    public List<ScheduleItemRequest> Items { get; set; } = new();
}

/// <summary>Sao chép lịch của một khoảng ngày (VD tuần trước) sang khoảng ngày khác — lịch mới ở dạng nháp.</summary>
public class CopySchedulesRequest
{
    /// <summary>Bắt buộc với Admin/HR; Quản lý bỏ trống = chi nhánh của mình.</summary>
    public int? BranchId { get; set; }

    /// <summary>Ngày đầu của khoảng nguồn.</summary>
    public DateOnly SourceFrom { get; set; }

    /// <summary>Ngày đầu của khoảng đích.</summary>
    public DateOnly TargetFrom { get; set; }

    [Range(1, 31, ErrorMessage = "Số ngày sao chép từ 1 đến 31.")]
    public int Days { get; set; } = 7;
}

/// <param name="Created">Số ca đã xếp.</param>
/// <param name="Skipped">Số ca bị bỏ qua (chỉ có khi sao chép lịch).</param>
/// <param name="SkippedReasons">Lý do bỏ qua (tối đa 20 dòng đầu).</param>
public record ScheduleBatchResult(int Created, int Skipped, IReadOnlyList<string> SkippedReasons);
