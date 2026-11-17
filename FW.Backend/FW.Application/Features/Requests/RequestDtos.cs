using System.ComponentModel.DataAnnotations;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Services;

namespace FW.Application.Features.Requests;

// ---------------------------------------------------------------- Loại đơn

public record LeaveTypeDto(
    int Id, string Name, string Category, string? Description, bool IsPaid, bool DeductsBalance, bool RequiresAttachment, bool IsActive)
{
    public static LeaveTypeDto From(LeaveType t) =>
        new(t.Id, t.Name, t.Category, t.Description, t.IsPaid, t.DeductsBalance, t.RequiresAttachment, t.IsActive);
}

public class LeaveTypeRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên loại đơn.")]
    [MaxLength(100, ErrorMessage = "Tên loại đơn tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>leave, shift_swap, overtime, explanation.</summary>
    [Required(ErrorMessage = "Vui lòng chọn nhóm đơn.")]
    public string Category { get; set; } = string.Empty;

    [MaxLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự.")]
    public string? Description { get; set; }

    public bool IsPaid { get; set; } = true;
    public bool DeductsBalance { get; set; }
    public bool RequiresAttachment { get; set; }
    public bool IsActive { get; set; } = true;
}

// ---------------------------------------------------------------- Đơn

/// <summary>
/// Tạo đơn (form-data, kèm "file" ảnh minh chứng nếu có). Điền các trường theo nhóm của loại đơn:
/// nghỉ → startDate, endDate; OT → startAt, endAt; đổi ca → sourceScheduleId + targetScheduleId (đổi ca) hoặc
/// targetUserId (nhờ làm thay); giải trình → attendanceLogId. Trường của nhóm khác bị bỏ qua.
/// </summary>
public class CreateLeaveRequestForm
{
    [Required(ErrorMessage = "Vui lòng chọn loại đơn.")]
    public int? LeaveTypeId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [MaxLength(RequestRules.MaxReasonLength, ErrorMessage = "Lý do tối đa 1000 ký tự.")]
    public string? Reason { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Giờ Việt Nam, VD 2026-11-20T18:00.</summary>
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public long? SourceScheduleId { get; set; }
    public long? TargetScheduleId { get; set; }
    public int? TargetUserId { get; set; }

    public long? AttendanceLogId { get; set; }
}

/// <summary>Đồng nghiệp trả lời đơn đổi ca (UC 5.6).</summary>
public class PeerResponseRequest
{
    [Required(ErrorMessage = "Vui lòng chọn đồng ý hoặc từ chối.")]
    public bool? Accepted { get; set; }

    [MaxLength(RequestRules.MaxNoteLength, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? Note { get; set; }
}

/// <summary>Bộ lọc danh sách đơn (query string).</summary>
public class LeaveRequestFilter : PagedQuery
{
    /// <summary>pending_peer, pending, approved, rejected, peer_declined, cancelled.</summary>
    public string? Status { get; set; }

    /// <summary>leave, shift_swap, overtime, explanation.</summary>
    public string? Category { get; set; }

    /// <summary>Chỉ dùng ở danh sách quản lý: Admin/HR lọc chi nhánh; Quản lý luôn là chi nhánh mình.</summary>
    public int? BranchId { get; set; }

    public int? UserId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public record RequestPersonDto(int Id, string EmployeeCode, string FullName, string? AvatarUrl);

public record RequestScheduleDto(long Id, DateTime WorkDate, string? ShiftName, string? StartTime, string? EndTime, string? Status);

public record RequestAttendanceDto(
    long Id, DateTime WorkDate, string? ShiftName, DateTime? CheckinTime, DateTime? CheckoutTime, int LateMinutes, int EarlyMinutes, string? Status);

public record LeaveRequestDto(
    int Id,
    RequestPersonDto Employee,
    int? BranchId,
    string? BranchName,
    int LeaveTypeId,
    string LeaveTypeName,
    string Category,
    bool IsPaid,
    bool DeductsBalance,
    DateTime StartDate,
    DateTime EndDate,
    decimal DurationDays,
    DateTime? StartAt,
    DateTime? EndAt,
    decimal? DurationHours,
    RequestPersonDto? TargetUser,
    RequestScheduleDto? SourceSchedule,
    RequestScheduleDto? TargetSchedule,
    DateTime? PeerRespondedAt,
    string? PeerNote,
    RequestAttendanceDto? AttendanceLog,
    string Reason,
    string? AttachmentUrl,
    string Status,
    int? ApprovedBy,
    string? ApproverName,
    DateTime? ApprovedAt,
    string? ApproverNote,
    DateTime CreatedAt,
    bool CanCancel,
    bool CanRespond,
    bool CanReview)
{
    /// <param name="canReview">Người đang xem duyệt được đơn này (tính ở service theo vai trò & phạm vi chi nhánh).</param>
    public static LeaveRequestDto From(LeaveRequestDetail d, int? currentUserId, bool canReview = false) => new(
        d.Id,
        new RequestPersonDto(d.UserId, d.EmployeeCode, d.FullName, d.AvatarUrl),
        d.BranchId, d.BranchName,
        d.LeaveTypeId, d.LeaveTypeName, d.Category, d.IsPaid, d.DeductsBalance,
        d.StartDate, d.EndDate, d.DurationDays, d.StartAt, d.EndAt, d.DurationHours,
        d.TargetUserId is { } targetId ? new RequestPersonDto(targetId, d.TargetEmployeeCode ?? "", d.TargetFullName ?? "", d.TargetAvatarUrl) : null,
        d.SourceScheduleId is { } sourceId && d.SourceWorkDate is { } sourceDate
            ? new RequestScheduleDto(sourceId, sourceDate, d.SourceShiftName, Time(d.SourceStartTime), Time(d.SourceEndTime), d.SourceScheduleStatus)
            : null,
        d.TargetScheduleId is { } targetScheduleId && d.TargetWorkDate is { } targetDate
            ? new RequestScheduleDto(targetScheduleId, targetDate, d.TargetShiftName, Time(d.TargetStartTime), Time(d.TargetEndTime), d.TargetScheduleStatus)
            : null,
        d.PeerRespondedAt, d.PeerNote,
        d.AttendanceLogId is { } logId && d.LogWorkDate is { } logDate
            ? new RequestAttendanceDto(logId, logDate, d.LogShiftName, d.LogCheckinTime, d.LogCheckoutTime,
                d.LogLateMinutes ?? 0, d.LogEarlyMinutes ?? 0, d.LogStatus)
            : null,
        d.Reason, d.AttachmentUrl, d.Status,
        d.ApprovedBy, d.ApproverName, d.ApprovedAt, d.ApproverNote, d.CreatedAt,
        CanCancel: d.UserId == currentUserId && Domain.Enums.RequestStatus.IsOpen(d.Status),
        CanRespond: d.TargetUserId == currentUserId && d.Status == Domain.Enums.RequestStatus.PendingPeer,
        CanReview: canReview);

    private static string? Time(TimeSpan? time) => time?.ToString(@"hh\:mm");
}

/// <summary>Một ca của đồng nghiệp có thể đổi lấy.</summary>
public record SwapShiftOptionDto(long ScheduleId, int UserId, string EmployeeCode, string FullName, DateTime WorkDate, string ShiftName,
    string StartTime, string EndTime);

/// <summary>Lựa chọn cho đơn đổi ca: ca của đồng nghiệp cùng chi nhánh (±7 ngày quanh ca muốn đổi) và đồng nghiệp có thể làm thay.</summary>
public record SwapOptionsDto(RequestScheduleDto Source, IReadOnlyList<SwapShiftOptionDto> Shifts, IReadOnlyList<RequestPersonDto> Colleagues);

/// <summary>Quỹ phép năm của tôi. <see cref="AvailableDays"/> = còn lại − đang chờ duyệt.</summary>
public record LeaveBalanceDto(int Year, decimal TotalDays, decimal UsedDays, decimal RemainingDays, decimal PendingDays, decimal AvailableDays,
    bool IsDefault);
