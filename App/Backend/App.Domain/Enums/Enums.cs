namespace App.Domain.Enums
{
    /// <summary>
    /// Trạng thái hoạt động của Chi nhánh
    /// </summary>
    public enum BranchStatus
    {
        Active,
        Inactive
    }

    /// <summary>
    /// Trạng thái hoạt động của Nhân viên / Người dùng
    /// </summary>
    public enum UserStatus
    {
        Active,
        Suspended,
        Left
    }

    /// <summary>
    /// Trạng thái lịch phân ca
    /// </summary>
    public enum ScheduleStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Absent,
        Cancelled
    }

    /// <summary>
    /// Trạng thái nhật ký chấm công sau đối soát
    /// </summary>
    public enum AttendanceStatus
    {
        Valid,
        PendingApproval,
        Invalid
    }

    /// <summary>
    /// Trạng thái xử lý đơn từ
    /// </summary>
    public enum LeaveRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    /// <summary>
    /// Phân loại phòng chat
    /// </summary>
    public enum ConversationType
    {
        Direct,
        Group
    }

    /// <summary>
    /// Phân loại thông báo hệ thống
    /// </summary>
    public enum NotificationType
    {
        Attendance,
        LeaveApproval,
        ScheduleNew,
        TaskAssigned,
        System
    }

    /// <summary>
    /// Trạng thái chốt bảng công tháng
    /// </summary>
    public enum TimesheetStatus
    {
        Draft,
        Locked,
        Finalized
    }
}
