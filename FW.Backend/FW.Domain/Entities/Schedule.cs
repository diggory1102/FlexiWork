using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Một ca được xếp cho một nhân viên vào một ngày (bảng schedules).
/// Lịch mới tạo ở dạng nháp (<see cref="IsPublished"/> = false): nhân viên chưa thấy và chưa chấm công được;
/// Quản lý công bố lịch để chốt. Lịch nháp xóa hẳn khỏi CSDL; lịch đã công bố chỉ được chuyển sang
/// <see cref="ScheduleStatus.Cancelled"/> để giữ lịch sử đối soát.
/// </summary>
public class Schedule : BaseEntity<long>, IAuditableEntity
{
    public int UserId { get; set; }
    public int ShiftId { get; set; }

    /// <summary>Chi nhánh nơi làm ca này (dùng để kiểm tra Geofence/Wi-Fi khi chấm công).</summary>
    public int BranchId { get; set; }

    /// <summary>Ngày làm việc (chỉ dùng phần ngày); ca qua ngày tính theo ngày bắt đầu ca.</summary>
    public DateTime WorkDate { get; set; }

    /// <summary>Giá trị thuộc <see cref="ScheduleStatus"/>.</summary>
    public string Status { get; set; } = ScheduleStatus.Scheduled;

    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Người xếp lịch (Quản lý / HR); null nếu tài khoản đó đã bị xóa.</summary>
    public int? CreatedBy { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
