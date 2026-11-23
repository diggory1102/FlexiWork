
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;

namespace FW.Application.Features.Assistant;

// =====================================================================================================
// CỔNG DỮ LIỆU NHÂN SỰ — đây là ĐIỂM TÍCH HỢP với FW.Backend.
// Khi các Task 3.2 / 4.x / 5.3 hoàn thiện, viết lớp cài đặt các interface này bằng chính service sẵn có
// (ScheduleService, LeaveService, TimesheetService...) thay cho FW.AI.InMemory / FW.AI.MySql.
// Mọi hàm nhận userId của NGƯỜI HỎI (lấy từ JWT), nên công cụ chỉ chạm tới dữ liệu của người đó.
// =====================================================================================================

public enum RequestKind
{
    Leave,
    ShiftSwap,
    Overtime,
    Explanation
}

public sealed record LeaveTypeDto(
    int Id,
    string Name,
    bool IsPaid,
    bool RequiresAttachment,
    RequestKind Category,
    bool ConsumesBalance);

public sealed record ScheduledShiftDto(
    long ScheduleId,
    DateOnly Date,
    int ShiftId,
    string ShiftName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal WorkUnits,
    string Status,
    int BranchId);

public sealed record TimesheetSummaryDto(
    int Year,
    int Month,
    decimal StandardWorkDays,
    decimal ActualWorkDays,
    int TotalLateMinutes,
    int TotalEarlyMinutes,
    decimal OvertimeHours,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    string Status);

public sealed record LeaveBalanceDto(
    int Year,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays,
    decimal PendingDays);

public sealed record LeaveRequestDto(
    int Id,
    int LeaveTypeId,
    string LeaveTypeName,
    RequestKind Category,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DurationDays,
    string Reason,
    string Status,
    DateTime CreatedAt,
    string? ApproverNote);

public sealed record ColleagueDto(int UserId, string FullName, string EmployeeCode);

public sealed record PendingRequestDto(
    int Id,
    int UserId,
    string EmployeeName,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DurationDays,
    string Reason,
    DateTime CreatedAt);

/// <summary>Lượt chấm công của chính người dùng — để chọn lượt cần giải trình.</summary>
public sealed record AttendanceLogDto(
    long Id,
    DateOnly Date,
    string ShiftName,
    TimeSpan? ShiftStartTime,
    DateTime CheckinTime,
    DateTime? CheckoutTime,
    int LateMinutes,
    int EarlyMinutes,
    string Status,
    bool NeedsExplanation);

public interface IHrDataService
{
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken ct = default);

    /// <summary>Lịch ĐÃ CÔNG BỐ và chưa hủy của chính người dùng trong khoảng ngày (gồm cả hai đầu).</summary>
    Task<IReadOnlyList<ScheduledShiftDto>> GetMyScheduleAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<TimesheetSummaryDto> GetMyTimesheetAsync(int userId, int year, int month, CancellationToken ct = default);

    Task<LeaveBalanceDto?> GetMyLeaveBalanceAsync(int userId, int year, CancellationToken ct = default);

    /// <summary>Lượt chấm công của chính người dùng trong khoảng ngày công (gồm cả hai đầu), cũ → mới.</summary>
    Task<IReadOnlyList<AttendanceLogDto>> GetMyAttendanceLogsAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Đơn của chính người dùng, mới nhất trước; <paramref name="status"/> null = mọi trạng thái.</summary>
    Task<IReadOnlyList<LeaveRequestDto>> GetMyRequestsAsync(int userId, string? status, int limit, CancellationToken ct = default);

    /// <summary>Đồng nghiệp cùng chi nhánh, đang hoạt động (trừ chính mình), tìm theo tên không phân biệt hoa/thường và dấu.</summary>
    Task<IReadOnlyList<ColleagueDto>> FindColleaguesAsync(int requesterId, string name, CancellationToken ct = default);

    /// <summary>
    /// Ca của đồng nghiệp trong một ngày — CHỈ phục vụ ghép đơn đổi ca (trả tối thiểu thông tin, chỉ khi cùng chi nhánh
    /// với người đề nghị). Không phải API xem lịch người khác.
    /// </summary>
    Task<IReadOnlyList<ScheduledShiftDto>> GetColleagueShiftsForSwapAsync(
        int requesterId, int colleagueId, DateOnly date, CancellationToken ct = default);

    /// <summary>Đơn đang chờ duyệt; <paramref name="branchId"/> null = toàn công ty (chỉ gọi cho HR/Admin).</summary>
    Task<IReadOnlyList<PendingRequestDto>> GetPendingRequestsAsync(int? branchId, int limit, CancellationToken ct = default);
}

/// <summary>
/// Dữ liệu đơn do nhân viên XÁC NHẬN — ánh xạ sang <c>CreateLeaveRequestForm</c> của hệ thống:
/// nghỉ → StartDate/EndDate; OT → StartAt/EndAt; đổi ca → SourceScheduleId + TargetScheduleId/TargetUserId; giải trình → AttendanceLogId.
/// </summary>
public sealed record LeaveRequestSubmission(
    int UserId,
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DurationDays,
    string Reason,
    int? TargetUserId,
    long? SourceScheduleId,
    long? TargetScheduleId,
    DateTime? StartAt = null,
    DateTime? EndAt = null,
    long? AttendanceLogId = null);

public sealed record SubmissionResult(int RequestId);

/// <summary>Ảnh minh chứng đính kèm lúc xác nhận (nếu loại đơn yêu cầu).</summary>
public sealed record SubmissionAttachment(Stream Content, string FileName, string ContentType, long Length);

/// <summary>Gửi đơn thật qua <c>ILeaveRequestService.CreateAsync</c> (dùng lại toàn bộ kiểm tra hợp lệ và thông báo cho Quản lý), KHÔNG ghi thẳng bảng.</summary>
public interface ILeaveRequestSubmitter
{
    Task<SubmissionResult> SubmitAsync(LeaveRequestSubmission submission, SubmissionAttachment? attachment, CancellationToken ct = default);
}
