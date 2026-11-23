using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;
using FW.Domain.Enums;
using FW.Domain.Services;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

/// <summary>
/// Gom hoạt động của nhân viên cho bộ chấm điểm nguy cơ nghỉ việc.
/// Vắng mặt KHÔNG đọc từ <c>schedules.status</c> (hệ thống không ghi 'absent') mà suy ra bằng đúng quy tắc của báo cáo vi phạm
/// (<see cref="AttendanceReportRules"/>): ca đã công bố, đã kết thúc, không có lượt chấm công và không nằm trong kỳ nghỉ đã duyệt.
/// Không phụ thuộc người dùng đăng nhập nên chạy được trong tác vụ nền hằng tuần.
/// </summary>
public sealed class AttritionDataSource : SessionStore, IAttritionDataSource
{
    private readonly IReportRepository _reports;
    private readonly IDateTimeProvider _clock;

    public AttritionDataSource(DbSession session, IReportRepository reports, IDateTimeProvider clock) : base(session)
    {
        _reports = reports;
        _clock = clock;
    }

    private sealed class EmployeeRow
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? BranchId { get; set; }
    }

    private sealed class RequestRow
    {
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal DurationDays { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    public async Task<IReadOnlyList<EmployeeActivity>> GetActivitiesAsync(DateOnly from, DateOnly toExclusive, CancellationToken ct = default)
    {
        var f = from.ToDateTime(TimeOnly.MinValue);
        var t = toExclusive.ToDateTime(TimeOnly.MinValue);
        var now = _clock.Now;

        var employees = await QueryAsync<EmployeeRow>(
            """
            SELECT u.`id` AS UserId, u.`full_name` AS Name, u.`branch_id` AS BranchId
            FROM `users` u
            WHERE u.`is_deleted` = 0 AND u.`status` = @Active
              AND EXISTS (SELECT 1 FROM `user_roles` ur JOIN `roles` r ON r.`id` = ur.`role_id`
                          WHERE ur.`user_id` = u.`id` AND r.`role_name` = @Role)
            """, new { Active = UserStatus.Active, Role = RoleNames.Employee }, ct);

        var shiftRows = (await _reports.GetShiftDaysAsync(f, t.AddDays(-1), branchId: null, userId: null, ct)).ToLookup(r => r.UserId);

        var requestRows = (await QueryAsync<RequestRow>(
            """
            SELECT lr.`user_id` AS UserId, lr.`created_at` AS CreatedAt, lr.`duration_days` AS DurationDays, lt.`category` AS Category
            FROM `leave_requests` lr JOIN `leave_types` lt ON lt.`id` = lr.`leave_type_id`
            WHERE lr.`is_deleted` = 0 AND lr.`status` NOT IN ('rejected', 'cancelled', 'peer_declined')
              AND lr.`created_at` >= @f AND lr.`created_at` < @t
            """, new { f, t }, ct)).ToLookup(r => r.UserId);

        return employees.Select(e =>
        {
            var shifts = new List<ShiftRecord>();
            foreach (var row in shiftRows[e.UserId])
            {
                var facts = new ShiftDayFacts(row.StartAt, row.EndAt, row.OnLeave, row.LogStatus, row.CheckoutTime, row.LateMinutes, row.EarlyMinutes,
                    row.LateGraceMinutes, row.EarlyLeaveGraceMinutes, row.Exempted);
                var state = AttendanceReportRules.StateOf(facts, now);
                var date = DateOnly.FromDateTime(row.WorkDate);
                switch (state)
                {
                    case ShiftDayState.Absent:
                        shifts.Add(new ShiftRecord(date, Attended: false, Absent: true, 0, 0));
                        break;
                    case ShiftDayState.Working or ShiftDayState.Completed:
                        // Đi muộn / về sớm đã được giải trình (đơn đã duyệt) thì không tính; về sớm chỉ xét khi đã check-out
                        var late = row.Exempted ? 0 : row.LateMinutes;
                        var early = row.Exempted || row.CheckoutTime is null ? 0 : row.EarlyMinutes;
                        shifts.Add(new ShiftRecord(date, Attended: true, Absent: false, late, early));
                        break;
                    // Chưa tới / đang chờ chấm công / nghỉ phép / lượt bị từ chối: chưa đủ căn cứ → bỏ qua
                }
            }

            var requests = requestRows[e.UserId]
                .Select(r => new RequestRecord(DateOnly.FromDateTime(r.CreatedAt), ToKind(r.Category), r.DurationDays))
                .ToList();

            // Hệ thống không có bảng đăng ký nguyện vọng ca (UC 3.5) → bỏ tín hiệu "giảm đăng ký ca" (bộ chấm tự bỏ qua khi null)
            return new EmployeeActivity(e.UserId, e.Name, e.BranchId, shifts, requests, null);
        }).ToList();
    }

    private static RequestKind ToKind(string category) => category switch
    {
        RequestCategory.ShiftSwap => RequestKind.ShiftSwap,
        RequestCategory.Overtime => RequestKind.Overtime,
        RequestCategory.Explanation => RequestKind.Explanation,
        _ => RequestKind.Leave
    };
}
