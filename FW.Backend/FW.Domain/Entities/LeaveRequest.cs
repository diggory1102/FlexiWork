using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>
/// Một đơn từ (bảng leave_requests): nghỉ theo ngày, đổi ca, làm thêm giờ hoặc giải trình — tùy nhóm của loại đơn.
/// Cột riêng của từng nhóm để null ở các nhóm khác.
/// </summary>
public class LeaveRequest : BaseAuditableEntity
{
    public int UserId { get; set; }

    /// <summary>Chi nhánh xử lý đơn: chi nhánh của người tạo (nghỉ, OT), của ca (đổi ca), của lượt chấm công (giải trình).</summary>
    public int? BranchId { get; set; }

    public int LeaveTypeId { get; set; }

    /// <summary>Ngày đầu / cuối mà đơn liên quan (chỉ dùng phần ngày) — để lọc theo khoảng ngày.</summary>
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Số ngày nghỉ (chỉ nhóm nghỉ; nhóm khác = 0).</summary>
    public decimal DurationDays { get; set; }

    // ---- Làm thêm giờ
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public decimal? DurationHours { get; set; }

    // ---- Đổi ca
    public int? TargetUserId { get; set; }
    public long? SourceScheduleId { get; set; }

    /// <summary>Ca nhận lại của đồng nghiệp; null = nhờ đồng nghiệp làm thay (không nhận ca nào).</summary>
    public long? TargetScheduleId { get; set; }
    public DateTime? PeerRespondedAt { get; set; }
    public string? PeerNote { get; set; }

    // ---- Giải trình
    public long? AttendanceLogId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentPublicId { get; set; }

    /// <summary>Giá trị thuộc <see cref="RequestStatus"/>.</summary>
    public string Status { get; set; } = RequestStatus.Pending;

    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApproverNote { get; set; }
}
