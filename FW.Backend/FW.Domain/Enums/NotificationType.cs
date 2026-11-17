namespace FW.Domain.Enums;

/// <summary>
/// Loại thông báo (cột notifications.type) kèm cách hiểu <c>reference_id</c> để ứng dụng mở đúng màn hình.
/// </summary>
public static class NotificationType
{
    /// <summary>Lượt chấm công cần duyệt (gửi người duyệt) hoặc kết quả duyệt (gửi nhân viên). reference_id = mã lượt chấm công.</summary>
    public const string Attendance = "attendance";

    /// <summary>Kết quả duyệt đơn (gửi người tạo đơn và đồng nghiệp trong đơn đổi ca). reference_id = mã đơn.</summary>
    public const string LeaveApproval = "leave_approval";

    /// <summary>Đơn mới chờ duyệt (gửi người duyệt) hoặc đề nghị / phản hồi đổi ca giữa đồng nghiệp. reference_id = mã đơn.</summary>
    public const string LeaveRequest = "leave_request";

    /// <summary>Lịch làm việc mới được công bố hoặc ca bị hủy. reference_id = ngày đầu của khoảng lịch (yyyy-MM-dd).</summary>
    public const string ScheduleNew = "schedule_new";

    /// <summary>Được giao checklist công việc khi vào ca. reference_id = mã lịch (schedule_id).</summary>
    public const string TaskAssigned = "task_assigned";

    /// <summary>Phiếu lương của kỳ đã chốt sẵn sàng. reference_id = mã kỳ lương.</summary>
    public const string Payslip = "payslip";

    public static readonly IReadOnlyList<string> All = new[] { Attendance, LeaveApproval, LeaveRequest, ScheduleNew, TaskAssigned, Payslip };

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}
