using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;

namespace FW.UnitTests.Assistant;

/// <summary>
/// Dữ liệu mẫu mô phỏng CSDL FlexiWork (cùng cấu trúc <c>flexiwork_seed.sql</c>): 2 chi nhánh, quản lý, nhân viên,
/// loại đơn, lịch ca quanh ngày hiện tại và 12 tuần lịch sử chấm công để chạy thử cảnh báo nghỉ việc.
/// Id nhân viên cố định để dễ thử: 3 = Lê Văn Nhân Viên (demo xin nghỉ), 5 = Nguyễn Thị Lan (đang xấu đi rõ),
/// 6 = Phạm Minh Tuấn (xấu đi nhẹ), 9 = Vũ Mai Anh (mới vào → chưa đủ dữ liệu).
/// </summary>
public static class DemoDataSeeder
{
    private sealed record ShiftDef(int Id, string Name, TimeSpan Start, TimeSpan End);

    private static readonly ShiftDef Morning = new(1, "Ca sáng chuẩn", new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0));
    private static readonly ShiftDef Afternoon = new(2, "Ca chiều chuẩn", new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0));
    private static readonly ShiftDef Evening = new(3, "Ca tối bán thời gian", new TimeSpan(18, 0, 0), new TimeSpan(22, 0, 0));

    public static void Seed(InMemoryHrData hr, InMemoryAttritionData attrition, DateOnly today)
    {
        SeedUsers(hr);
        SeedLeaveTypes(hr);

        long scheduleId = 1;
        var thisMonday = RelativeDateResolver.StartOfWeek(today);

        // ----- Lịch ca: từ 2 tuần trước tới 3 tuần sau -----
        foreach (var u in hr.Users.Where(u => u.Roles.Contains("ROLE_EMPLOYEE")))
        {
            for (var d = thisMonday.AddDays(-14); d < thisMonday.AddDays(21); d = d.AddDays(1))
            {
                foreach (var shift in ShiftsFor(u.Id, d))
                {
                    hr.Schedules.Add((u.Id, new ScheduledShiftDto(
                        scheduleId++, d, shift.Id, shift.Name, shift.Start, shift.End, 1.00m,
                        d < today ? "completed" : "scheduled", u.BranchId ?? 1)));
                }
            }

            hr.Balances[(u.Id, today.Year)] = new LeaveBalanceDto(
                today.Year, 12m, u.Id == 3 ? 1m : 0m, u.Id == 3 ? 11m : 12m, 0m);
        }

        SeedTimesheets(hr, today);
        SeedAttendanceLogs(hr, today);
        SeedRequests(hr, today);
        SeedAttritionHistory(hr, attrition, thisMonday);
    }

    // ------------------------------------------------------------------------------------------------
    private static void SeedUsers(InMemoryHrData hr)
    {
        hr.Users.AddRange(new[]
        {
            new DemoUser(1, "EMP001", "Nguyễn Quản Trị", 1, new[] { "ROLE_ADMIN" }),
            new DemoUser(2, "EMP002", "Trần Quản Lý", 1, new[] { "ROLE_MANAGER" }),
            new DemoUser(3, "EMP003", "Lê Văn Nhân Viên", 1, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(4, "EMP004", "Phạm Nhân Sự", null, new[] { "ROLE_HR" }),
            new DemoUser(5, "EMP005", "Nguyễn Thị Lan", 1, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(6, "EMP006", "Phạm Minh Tuấn", 1, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(7, "EMP007", "Hoàng Thu Hà", 1, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(8, "EMP008", "Đỗ Quang Huy", 1, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(9, "EMP009", "Vũ Mai Anh", 1, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(10, "EMP010", "Bùi Văn Nam", 2, new[] { "ROLE_EMPLOYEE" }),
            new DemoUser(11, "EMP011", "Lý Quản Lý Hoàn Kiếm", 2, new[] { "ROLE_MANAGER" })
        });
    }

    private static void SeedLeaveTypes(InMemoryHrData hr)
    {
        hr.LeaveTypes.AddRange(new[]
        {
            new LeaveTypeDto(1, "Nghỉ phép năm (Annual Leave)", true, false, RequestKind.Leave, true),
            new LeaveTypeDto(2, "Nghỉ ốm / Khám chữa bệnh", true, true, RequestKind.Leave, false),
            new LeaveTypeDto(3, "Nghỉ việc riêng không hưởng lương", false, false, RequestKind.Leave, false),
            new LeaveTypeDto(4, "Đơn xin đổi ca làm việc", true, false, RequestKind.ShiftSwap, false),
            new LeaveTypeDto(5, "Đơn xin làm thêm giờ (OT)", true, false, RequestKind.Overtime, false),
            new LeaveTypeDto(6, "Giải trình đi muộn / về sớm", true, false, RequestKind.Explanation, false)
        });
    }

    /// <summary>Ca của nhân viên trong một ngày (Thứ Hai–Thứ Bảy; Chủ nhật nghỉ).</summary>
    private static IEnumerable<ShiftDef> ShiftsFor(int userId, DateOnly date)
    {
        if (date.DayOfWeek == DayOfWeek.Sunday) yield break;

        switch (userId)
        {
            case 3: // luôn có cả ca sáng và chiều → "nghỉ ca sáng" = 0,5 ngày
                yield return Morning;
                yield return Afternoon;
                break;
            case 9: // người mới
                yield return Morning;
                break;
            default:
                if (userId % 2 == 0) { yield return Morning; yield return Afternoon; }
                else { yield return Afternoon; yield return Evening; }
                break;
        }
    }

    private static void SeedTimesheets(InMemoryHrData hr, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        foreach (var u in hr.Users.Where(u => u.Roles.Contains("ROLE_EMPLOYEE")))
        {
            var rng = new Random(1000 + u.Id);
            var worked = hr.Schedules
                .Where(s => s.UserId == u.Id && s.Shift.Date >= monthStart && s.Shift.Date < today)
                .Sum(s => s.Shift.WorkUnits);

            hr.Timesheets[(u.Id, today.Year, today.Month)] = new TimesheetSummaryDto(
                today.Year, today.Month, 26m, worked, // công = tổng hệ số công (work_units) của các ca đã làm
                rng.Next(0, 40), rng.Next(0, 15), 0m, u.Id == 3 ? 1m : 0m, 0m, "draft");
        }
    }

    /// <summary>Hôm qua nhân viên 3 đi muộn 12 phút ca sáng (cần giải trình), ca chiều đúng giờ.</summary>
    private static void SeedAttendanceLogs(InMemoryHrData hr, DateOnly today)
    {
        var yesterday = today.AddDays(-1);
        if (yesterday.DayOfWeek == DayOfWeek.Sunday) return;
        hr.AttendanceLogs.Add((3, new AttendanceLogDto(9001, yesterday, Morning.Name, Morning.Start, yesterday.ToDateTime(TimeOnly.MinValue).AddHours(8).AddMinutes(12),
            yesterday.ToDateTime(TimeOnly.MinValue).AddHours(12), 12, 0, "valid", true)));
        hr.AttendanceLogs.Add((3, new AttendanceLogDto(9002, yesterday, Afternoon.Name, Afternoon.Start, yesterday.ToDateTime(TimeOnly.MinValue).AddHours(13),
            yesterday.ToDateTime(TimeOnly.MinValue).AddHours(17), 0, 0, "valid", false)));
    }

    private static void SeedRequests(InMemoryHrData hr, DateOnly today)
    {
        var lastMonth = today.AddMonths(-1);
        hr.Requests.Add((3, new LeaveRequestDto(
            501, 1, "Nghỉ phép năm (Annual Leave)", RequestKind.Leave, lastMonth, lastMonth, 1m,
            "Việc gia đình", "approved", lastMonth.ToDateTime(TimeOnly.MinValue).AddDays(-2), "Đồng ý")));

        var nextWeekWed = RelativeDateResolver.Resolve(today, DayOfWeek.Wednesday, 1);
        hr.Requests.Add((5, new LeaveRequestDto(
            502, 3, "Nghỉ việc riêng không hưởng lương", RequestKind.Leave, nextWeekWed, nextWeekWed, 1m,
            "Có việc đột xuất", "pending", DateTime.UtcNow.AddHours(7).AddHours(-5), null)));
    }

    // ------------------------------------------------------------------------------------------------
    // 12 tuần lịch sử trước tuần hiện tại cho cảnh báo nghỉ việc
    // ------------------------------------------------------------------------------------------------
    private static void SeedAttritionHistory(InMemoryHrData hr, InMemoryAttritionData attrition, DateOnly thisMonday)
    {
        const int historyWeeks = 12;

        foreach (var u in hr.Users.Where(u => u.Roles.Contains("ROLE_EMPLOYEE")))
        {
            var rng = new Random(42 + u.Id);
            var shifts = new List<ShiftRecord>();
            var requests = new List<RequestRecord>();
            var availability = new List<AvailabilityRecord>();

            var weeks = u.Id == 9 ? 3 : historyWeeks; // người mới chỉ có 3 tuần
            for (var w = weeks; w >= 1; w--)
            {
                var weekStart = thisMonday.AddDays(-7 * w);
                var isRecent = w <= 2;

                for (var i = 0; i < 6; i++) // Thứ Hai–Thứ Bảy
                {
                    var date = weekStart.AddDays(i);
                    foreach (var _ in ShiftsFor(u.Id, date))
                    {
                        shifts.Add(MakeShift(u.Id, date, isRecent, rng));
                    }
                }

                availability.Add(new AvailabilityRecord(weekStart, u.Id == 5 && isRecent ? 1 : 4));

                if (u.Id == 5 && isRecent)
                {
                    requests.Add(new RequestRecord(weekStart.AddDays(1), RequestKind.Leave, 1m));
                    requests.Add(new RequestRecord(weekStart.AddDays(3), RequestKind.Leave, 1.5m));
                    requests.Add(new RequestRecord(weekStart.AddDays(4), RequestKind.ShiftSwap, 0m));
                }
                else if (u.Id == 6 && w == 1)
                {
                    requests.Add(new RequestRecord(weekStart.AddDays(2), RequestKind.Leave, 1m));
                }
                else if (rng.NextDouble() < 0.08)
                {
                    requests.Add(new RequestRecord(weekStart.AddDays(rng.Next(0, 5)), RequestKind.Leave, 0.5m));
                }
            }

            attrition.Activities.Add(new EmployeeActivity(u.Id, u.FullName, u.BranchId, shifts, requests, availability));
        }
    }

    private static ShiftRecord MakeShift(int userId, DateOnly date, bool isRecent, Random rng)
    {
        switch (userId)
        {
            case 5 when isRecent: // đang xấu đi rõ rệt
            {
                if (rng.NextDouble() < 0.18) return new ShiftRecord(date, false, true, 0, 0);
                var late = rng.NextDouble() < 0.65 ? rng.Next(10, 26) : 0;
                return new ShiftRecord(date, true, false, late, rng.NextDouble() < 0.25 ? rng.Next(5, 20) : 0);
            }
            case 6 when isRecent: // xấu đi vừa phải: muộn nhiều hơn, thỉnh thoảng về sớm
            {
                var late = rng.NextDouble() < 0.50 ? rng.Next(8, 21) : 0;
                return new ShiftRecord(date, true, false, late, rng.NextDouble() < 0.20 ? rng.Next(5, 16) : 0);
            }
            default: // ổn định: thỉnh thoảng muộn vài phút
            {
                var late = rng.NextDouble() < 0.08 ? rng.Next(3, 9) : 0;
                return new ShiftRecord(date, true, false, late, 0);
            }
        }
    }
}
