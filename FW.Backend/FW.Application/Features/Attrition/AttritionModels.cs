
using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;

namespace FW.Application.Features.Attrition;

public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>Một ca ĐÃ QUA hạn của nhân viên: hoặc đã chấm công, hoặc bị đánh dấu vắng. Ca chưa tới / đã hủy không đưa vào.</summary>
public sealed record ShiftRecord(DateOnly Date, bool Attended, bool Absent, int LateMinutes, int EarlyMinutes);

/// <summary>Một đơn nhân viên đã nộp (bỏ đơn bị từ chối/hủy). <see cref="Days"/> chỉ có nghĩa với đơn nghỉ.</summary>
public sealed record RequestRecord(DateOnly SubmittedOn, RequestKind Category, decimal Days);

/// <summary>Số ca / khung giờ nhân viên đăng ký làm được trong một tuần (UC 3.5 — tùy chọn).</summary>
public sealed record AvailabilityRecord(DateOnly WeekStart, int Slots);

public sealed record EmployeeActivity(
    int UserId,
    string Name,
    int? BranchId,
    IReadOnlyList<ShiftRecord> Shifts,
    IReadOnlyList<RequestRecord> Requests,
    IReadOnlyList<AvailabilityRecord>? Availability);

/// <summary>Kết quả một tín hiệu: giá trị 2 tuần gần nhất so với nền của chính nhân viên.</summary>
public sealed record SignalResult(
    string Key,
    string Label,
    double Recent,
    double Baseline,
    double SubScore,
    double Weight,
    double Contribution,
    string? Reason);

public sealed record AttritionAssessment(
    int Score,
    RiskLevel Level,
    bool DataSufficient,
    IReadOnlyList<SignalResult> Signals,
    IReadOnlyList<string> Reasons);

public sealed record AttritionSnapshot(
    int UserId,
    string EmployeeName,
    int? BranchId,
    DateOnly WeekStart,
    int Score,
    RiskLevel Level,
    bool DataSufficient,
    int? PreviousScore,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<SignalResult> Signals,
    DateTime ComputedAt)
{
    /// <summary>Thay đổi điểm so với tuần trước (null nếu chưa có tuần trước).</summary>
    public int? Delta => PreviousScore is { } p ? Score - p : null;
}

public sealed record AttritionRunSummary(DateOnly WeekStart, int Employees, int Scored, int Medium, int High);

public sealed record AttritionCaller(int UserId, int? BranchId, IReadOnlyList<string> Roles);

// ----- Cổng dữ liệu (điểm tích hợp) -----

public interface IAttritionDataSource
{
    /// <summary>
    /// Hoạt động của MỌI nhân viên đang làm trong [from, toExclusive). Khi tích hợp: gom từ schedules + attendance_logs +
    /// leave_requests (xem docs/INTEGRATION.md). Chỉ trả nhân viên (không gồm Admin/HR).
    /// </summary>
    Task<IReadOnlyList<EmployeeActivity>> GetActivitiesAsync(DateOnly from, DateOnly toExclusive, CancellationToken ct = default);
}

public interface IAttritionSnapshotStore
{
    /// <summary>Ghi đè theo (user, weekStart) — chạy lại cùng tuần không sinh bản ghi trùng.</summary>
    Task UpsertAsync(IReadOnlyList<AttritionSnapshot> snapshots, CancellationToken ct = default);

    /// <summary>Bản ghi mới nhất của từng nhân viên (theo tuần), lọc chi nhánh nếu có.</summary>
    Task<IReadOnlyList<AttritionSnapshot>> GetLatestAsync(int? branchId, CancellationToken ct = default);

    /// <summary>Lịch sử của một nhân viên, tuần cũ → mới.</summary>
    Task<IReadOnlyList<AttritionSnapshot>> GetHistoryAsync(int userId, int weeks, CancellationToken ct = default);
}
