// Nạp dữ liệu DEMO quy mô 30–80 nhân sự (Task 6.4): nhiều chi nhánh, lịch làm việc ~12 tuần, lịch sử chấm công, đơn nghỉ / OT / giải trình / đổi ca,
// kèm vài nhân viên "xấu dần" (đi muộn, vắng, xin nghỉ tăng ở các tuần gần đây) để minh họa cảnh báo nghỉ việc (Task 7.4).
//
//   dotnet run -c Release --project FW.Backend/tools/DemoDataSeeder -- --connection "<chuỗi kết nối>" [tùy chọn]
//
// Tùy chọn:  --connection <chuỗi>   hoặc biến môi trường FW_SEED_DB
//            --password <mật khẩu>  mật khẩu chung của mọi tài khoản demo (hoặc biến FW_DEMO_PASSWORD). Chỉ CSDL local mới được dùng mặc định Admin@123.
//            --employees N          số nhân viên (30–80, mặc định 60)         --weeks-back N (mặc định 10)   --weeks-ahead N (mặc định 2)
//            --seed N               hạt giống ngẫu nhiên (mặc định 20261122; cùng hạt giống + cùng ngày chạy → cùng dữ liệu)
//            --today yyyy-MM-dd     coi hôm nay là ngày này (mặc định hôm nay)
//            --reset-only           chỉ xóa dữ liệu demo cũ, không tạo mới
//            --yes                  bắt buộc khi CSDL không phải localhost (tránh nạp nhầm lên CSDL thật)
//
// Dữ liệu demo nhận biết bằng mã nhân viên bắt đầu bằng "DM" và email @demo.flexiwork.vn; chạy lại sẽ xóa bộ cũ rồi tạo mới (không đụng dữ liệu khác).
using System.Globalization;
using DemoDataSeeder;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using FW.Infrastructure.Database;
using MySqlConnector;

string? Opt(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

int IntOpt(string name, int fallback) => Opt(name) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

var connectionString = Opt("--connection") ?? Environment.GetEnvironmentVariable("FW_SEED_DB");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Thiếu chuỗi kết nối: dùng --connection \"...\" hoặc đặt biến môi trường FW_SEED_DB.");
    return 1;
}

var builder = new MySqlConnectionStringBuilder(connectionString);
var isLocal = builder.Server is "localhost" or "127.0.0.1" or "::1";
var password = Opt("--password") ?? Environment.GetEnvironmentVariable("FW_DEMO_PASSWORD") ?? (isLocal ? "Admin@123" : null);
var resetOnly = args.Contains("--reset-only");
if (!resetOnly)
{
    if (password == null)
    {
        Console.Error.WriteLine("CSDL không phải localhost: phải đặt mật khẩu demo riêng bằng --password (hoặc biến FW_DEMO_PASSWORD).");
        return 1;
    }
    if (!isLocal && (password == "Admin@123" || password.Length < 8))
    {
        Console.Error.WriteLine("Mật khẩu demo cho CSDL trên mạng phải dài từ 8 ký tự và KHÔNG được là mật khẩu công khai trong repo (Admin@123).");
        return 1;
    }
}
if (!isLocal && !args.Contains("--yes"))
{
    Console.Error.WriteLine($"Sắp {(resetOnly ? "XÓA dữ liệu demo trên" : "nạp dữ liệu demo lên")} {builder.Server} / {builder.Database}. Chạy lại kèm --yes để xác nhận.");
    return 1;
}

var employeeCount = Math.Clamp(IntOpt("--employees", 60), 30, 80);
var weeksBack = Math.Clamp(IntOpt("--weeks-back", 10), 3, 20);
var weeksAhead = Math.Clamp(IntOpt("--weeks-ahead", 2), 1, 6);
var seed = IntOpt("--seed", 20261122);
var today = Opt("--today") is { } t ? DateTime.ParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTime.Today;
var now = today == DateTime.Today ? DateTime.Now : today.AddHours(23);
var random = new Random(seed);

static DateTime MondayOf(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));
var thisMonday = MondayOf(today);
var windowStart = thisMonday.AddDays(-7 * weeksBack);
var windowEnd = thisMonday.AddDays(7 * weeksAhead + 6);
int WeeksFromEnd(DateTime date) => (int)((thisMonday - MondayOf(date)).TotalDays / 7);

Console.WriteLine($"Đích: {builder.Server} / {builder.Database}  ({(isLocal ? "local" : "TRÊN MẠNG")})");
Console.WriteLine($"Khoảng lịch: {windowStart:dd/MM/yyyy} → {windowEnd:dd/MM/yyyy} (hôm nay {today:dd/MM/yyyy})");

await using var session = new DbSession(new MySqlDbConnectionFactory(connectionString));
var db = new Db(session);

await session.ExecuteInTransactionAsync(async _ =>
{
    // ---------------------------------------------------------------- 0. Xóa bộ demo cũ
    var oldIds = await db.QueryAsync<int>("SELECT id FROM users WHERE employee_code LIKE 'DM%' AND email LIKE '%@demo.flexiwork.vn'");
    if (oldIds.Count > 0)
    {
        var p = new { Ids = oldIds };
        await db.ExecAsync("DELETE FROM shift_task_assignments WHERE assigned_to IN @Ids OR schedule_id IN (SELECT id FROM schedules WHERE user_id IN @Ids)", p);
        await db.ExecAsync("DELETE FROM leave_requests WHERE user_id IN @Ids OR target_user_id IN @Ids", p);
        await db.ExecAsync("DELETE FROM attendance_logs WHERE user_id IN @Ids", p);
        await db.ExecAsync("DELETE FROM schedules WHERE user_id IN @Ids", p);
        await db.ExecAsync("DELETE FROM payslip_items WHERE payslip_id IN (SELECT id FROM payslips WHERE user_id IN @Ids)", p);
        foreach (var table in new[] { "payslips", "monthly_timesheets", "user_allowances", "leave_balance_adjustments", "leave_balances", "notifications", "conversation_members", "face_profiles", "user_roles" })
        {
            await db.ExecAsync($"DELETE FROM {table} WHERE user_id IN @Ids", p);
        }
        await db.ExecAsync("DELETE FROM messages WHERE sender_id IN @Ids", p);
        await db.ExecAsync("DELETE FROM users WHERE id IN @Ids", p);
        Console.WriteLine($"Đã xóa bộ demo cũ ({oldIds.Count} tài khoản).");
    }
    if (resetOnly) return 0;

    // ---------------------------------------------------------------- 1. Dữ liệu tham chiếu (dùng cái có sẵn, thiếu thì tạo)
    var roles = (await db.QueryAsync<IdName>("SELECT id AS Id, role_name AS Name FROM roles")).ToDictionary(r => r.Name, r => r.Id);
    foreach (var needed in new[] { RoleNames.Employee, RoleNames.Manager })
    {
        if (!roles.ContainsKey(needed)) throw new InvalidOperationException($"Thiếu vai trò {needed} — chạy script seed vai trò trước.");
    }

    var leaveTypes = await db.QueryAsync<LeaveTypeRow>(
        "SELECT id AS Id, category AS Category, deducts_balance AS DeductsBalance, requires_attachment AS RequiresAttachment FROM leave_types WHERE is_deleted = 0");
    var annual = leaveTypes.FirstOrDefault(l => l.Category == "leave" && l.DeductsBalance);
    var sick = leaveTypes.FirstOrDefault(l => l.Category == "leave" && l.RequiresAttachment);
    var swapType = leaveTypes.FirstOrDefault(l => l.Category == "shift_swap");
    var overtimeType = leaveTypes.FirstOrDefault(l => l.Category == "overtime");
    var explanationType = leaveTypes.FirstOrDefault(l => l.Category == "explanation");

    var branches = await EnsureBranchesAsync(db);
    var shifts = await EnsureShiftsAsync(db);
    var departmentId = await db.ScalarAsync<int?>("SELECT id FROM departments WHERE code = 'DEP-OPS' LIMIT 1")
                       ?? await db.ScalarAsync<int>("INSERT INTO departments (code, name, description) VALUES ('DM-OPS', 'Khối Vận hành (demo)', 'Dữ liệu demo'); SELECT LAST_INSERT_ID();");
    var managerPosition = await EnsurePositionAsync(db, "%Quản lý Cửa hàng%", "Quản lý Cửa hàng (Store Manager)", 80000m);
    var salesPosition = await EnsurePositionAsync(db, "%Bán hàng%", "Nhân viên Bán hàng / Thu ngân", 35000m);
    var baristaPosition = await EnsurePositionAsync(db, "%Pha chế%", "Nhân viên Pha chế / Kỹ thuật", 40000m);

    // ---------------------------------------------------------------- 2. Tài khoản
    var hash = BCrypt.Net.BCrypt.HashPassword(password!, 11);
    var usedNames = new HashSet<string>();
    var people = new List<Person>();
    var weights = new[] { 0.34, 0.26, 0.20, 0.20 };
    var perBranch = weights.Select(w => (int)Math.Round(employeeCount * w)).ToArray();
    perBranch[0] += employeeCount - perBranch.Sum();

    User NewUser(string code, string fullName, Branch branch, int positionId, bool fullTime) => new()
    {
        EmployeeCode = code,
        Email = $"{code.ToLowerInvariant()}@demo.flexiwork.vn",
        PasswordHash = hash,
        FullName = fullName,
        PhoneNumber = "09" + random.Next(10_000_000, 99_999_999),
        BranchId = branch.Id,
        DepartmentId = departmentId,
        PositionId = positionId,
        EmploymentType = fullTime ? EmploymentType.FullTime : EmploymentType.PartTime,
        HireDate = today.AddDays(-random.Next(60, 900)),
        BaseSalary = fullTime ? 8_000_000m + 500_000m * random.Next(0, 9) : null,
        Status = UserStatus.Active,
        MustChangePassword = false,
        PasswordChangedAt = now
    };

    for (var b = 0; b < branches.Count; b++)
    {
        people.Add(new Person { User = NewUser($"DMQ{b + 1}", Names.Next(random, usedNames), branches[b].Branch, managerPosition, true), Branch = branches[b].Branch, Trait = Trait.Reliable, Works = false });
    }

    var employees = new List<Person>();
    var index = 0;
    for (var b = 0; b < branches.Count; b++)
    {
        for (var i = 0; i < perBranch[b]; i++)
        {
            index++;
            var fullTime = random.Chance(0.6);
            var user = NewUser($"DM{index:000}", Names.Next(random, usedNames), branches[b].Branch, random.Chance(0.7) ? salesPosition : baristaPosition, fullTime);
            employees.Add(new Person { User = user, Branch = branches[b].Branch, Trait = Trait.Reliable, PreferredShiftIndex = random.Next(3) });
        }
    }

    // Vài người đã nghỉ việc / bị khóa (không có lịch) cho danh sách nhân sự đa dạng
    var inactive = employees.OrderBy(_ => random.Next()).Take(3).ToList();
    for (var i = 0; i < inactive.Count; i++)
    {
        inactive[i].User.Status = i < 2 ? UserStatus.Left : UserStatus.Suspended;
    }

    // Tính cách chấm công: chia đều "xấu dần" qua các chi nhánh (5–8 người, ~10%)
    var active = employees.Except(inactive).ToList();
    var decliningTarget = Math.Clamp((int)Math.Round(active.Count * 0.10), 5, 8);
    var declining = new List<Person>();
    foreach (var group in active.GroupBy(e => e.Branch.Id).Select(g => g.OrderBy(_ => random.Next()).ToList()).ToList())
    {
        if (declining.Count < decliningTarget) declining.Add(group[0]);
    }
    foreach (var e in active.Except(declining).OrderBy(_ => random.Next()))
    {
        if (declining.Count < decliningTarget) declining.Add(e);
    }
    var rest = active.Except(declining).OrderBy(_ => random.Next()).ToList();
    var oftenLate = rest.Take((int)Math.Round(rest.Count * 0.08)).ToHashSet();
    var average = rest.Skip(oftenLate.Count).Take((int)Math.Round(rest.Count * 0.30)).ToHashSet();
    var decliningSet = declining.ToHashSet();
    var finalEmployees = new List<Person>();
    foreach (var e in employees)
    {
        var trait = decliningSet.Contains(e) ? Trait.Declining : oftenLate.Contains(e) ? Trait.OftenLate : average.Contains(e) ? Trait.Average : Trait.Reliable;
        finalEmployees.Add(new Person
        {
            User = e.User, Branch = e.Branch, Trait = trait, PreferredShiftIndex = e.PreferredShiftIndex, Works = !inactive.Contains(e)
        });
    }
    people.AddRange(finalEmployees);

    foreach (var person in people)
    {
        person.Id = await db.InsertAsync<User, int>("users", person.User);
        await db.ExecAsync("INSERT INTO user_roles (user_id, role_id) VALUES (@U, @R)", new { U = person.Id, R = roles[person.User.EmployeeCode.StartsWith("DMQ", StringComparison.Ordinal) ? RoleNames.Manager : RoleNames.Employee] });
    }
    var managerOf = people.Where(p => p.User.EmployeeCode.StartsWith("DMQ", StringComparison.Ordinal)).ToDictionary(p => p.Branch.Id, p => p.Id);
    var workers = people.Where(p => p.Works).ToList();

    // ---------------------------------------------------------------- 3. Lịch làm việc (chưa ghi) — mỗi tuần từng người
    var slots = new List<Slot>();
    for (var week = 0; week < weeksBack + weeksAhead + 1; week++)
    {
        var monday = windowStart.AddDays(7 * week);
        foreach (var person in workers)
        {
            var days = Enumerable.Range(0, 7).OrderBy(_ => random.Next()).ToList();
            if (person.FullTime)
            {
                foreach (var d in days.Take(5))
                {
                    var roll = random.NextDouble();
                    var shiftIndexes = roll < 0.75 ? new[] { 0, 1 } : roll < 0.87 ? new[] { 0 } : new[] { 1 };
                    foreach (var s in shiftIndexes) slots.Add(NewSlot(person, monday.AddDays(d), s));
                }
            }
            else
            {
                foreach (var d in days.Take(random.Chance(0.5) ? 3 : 4))
                {
                    slots.Add(NewSlot(person, monday.AddDays(d), random.Chance(0.75) ? person.PreferredShiftIndex : random.Next(3)));
                }
            }
        }
    }

    Slot NewSlot(Person person, DateTime date, int shiftIndex)
    {
        var published = date <= today.AddDays(7);
        return new Slot
        {
            Person = person, Date = date.Date, ShiftIndex = shiftIndex,
            Schedule = new Schedule
            {
                ShiftId = shifts[shiftIndex].Id, BranchId = person.Branch.Id, WorkDate = date.Date, Status = ScheduleStatus.Scheduled,
                IsPublished = published, PublishedAt = published ? Min(date.AddDays(-7).AddHours(9), now) : null, CreatedBy = managerOf[person.Branch.Id]
            }
        };
    }

    // ---------------------------------------------------------------- 4. Đơn nghỉ (quyết định trước để ca ngày nghỉ không có chấm công)
    var leaves = new List<LeaveRequest>();
    var usedLeave = workers.ToDictionary(w => w.Id, _ => 0m);
    string[] annualReasons = { "Về quê có việc gia đình", "Đi khám sức khỏe định kỳ", "Việc riêng cần giải quyết", "Đưa con đi học", "Nghỉ phép năm đã đăng ký từ trước" };
    string[] sickReasons = { "Sốt, đau họng, có giấy khám", "Đau dạ dày, bác sĩ cho nghỉ 1 ngày", "Cảm cúm, được kê đơn thuốc" };

    void AddLeave(Person person, DateTime date, bool? upcoming = null)
    {
        var personSlots = slots.Where(s => s.Person == person && s.Date == date.Date).ToList();
        if (personSlots.Count == 0 || (annual == null && sick == null)) return;

        var isSick = sick != null && (annual == null || random.Chance(0.18));
        var type = isSick ? sick! : annual!;
        var future = upcoming ?? date.Date > today;
        var status = future ? (random.Chance(0.65) ? RequestStatus.Pending : RequestStatus.Approved) : (random.Chance(0.88) ? RequestStatus.Approved : RequestStatus.Rejected);
        var approver = managerOf[person.Branch.Id];
        leaves.Add(new LeaveRequest
        {
            UserId = person.Id, BranchId = person.Branch.Id, LeaveTypeId = type.Id, StartDate = date.Date, EndDate = date.Date, DurationDays = 1,
            Reason = random.PickOne(isSick ? sickReasons : annualReasons),
            AttachmentUrl = type.RequiresAttachment ? "https://res.cloudinary.com/demo/image/upload/w_600/sample.jpg" : null,
            Status = status,
            ApprovedBy = status == RequestStatus.Pending ? null : approver,
            ApprovedAt = status == RequestStatus.Pending ? null : Min(date.AddDays(-2).AddHours(10), now),
            ApproverNote = status == RequestStatus.Rejected ? "Ngày đó thiếu người, vui lòng chọn ngày khác." : null
        });
        if (status != RequestStatus.Approved) return;

        foreach (var s in personSlots) s.OnLeave = true;
        if (type.DeductsBalance) usedLeave[person.Id] += 1;
    }

    DateTime? PickDate(Person person, DateTime from, DateTime to)
    {
        var dates = slots.Where(s => s.Person == person && s.Date >= from.Date && s.Date <= to.Date && !s.OnLeave).Select(s => s.Date).Distinct().ToList();
        return dates.Count == 0 ? null : random.PickOne(dates);
    }

    foreach (var person in workers)
    {
        if (person.Trait == Trait.Declining)
        {
            // Nền: không xin nghỉ; từ 3 tuần gần đây: mỗi tuần một đơn, tuần này thêm đơn cho tuần tới
            foreach (var weeksFromEnd in new[] { 2, 1, 0 })
            {
                var monday = thisMonday.AddDays(-7 * weeksFromEnd);
                var date = PickDate(person, monday, weeksFromEnd == 0 ? today.AddDays(-1) : monday.AddDays(6));
                if (date != null) AddLeave(person, date.Value);
            }
            var next = PickDate(person, today.AddDays(1), thisMonday.AddDays(13));
            if (next != null) AddLeave(person, next.Value, upcoming: true);
        }
        else
        {
            var count = random.Chance(0.35) ? (random.Chance(0.2) ? 2 : 1) : 0;
            for (var i = 0; i < count; i++)
            {
                var upcoming = random.Chance(0.2);
                var date = upcoming ? PickDate(person, today.AddDays(1), windowEnd) : PickDate(person, windowStart.AddDays(7), today.AddDays(-1));
                if (date != null) AddLeave(person, date.Value);
            }
        }
    }

    // ---------------------------------------------------------------- 5. Chấm công cho các ca đã / đang diễn ra
    var logs = new List<(Slot Slot, AttendanceLog Log)>();
    const string imageUrl = "https://res.cloudinary.com/demo/image/upload/w_400/sample.jpg";
    foreach (var slot in slots)
    {
        var shift = shifts[slot.ShiftIndex];
        slot.Schedule.Status = slot.OnLeave ? ScheduleStatus.OnLeave : ScheduleStatus.Scheduled;
        if (slot.OnLeave || slot.Date > today) continue;

        var person = slot.Person;
        var rates = person.RatesFor(WeeksFromEnd(slot.Date));
        var start = shift.StartAt(slot.Date);
        var end = shift.EndAt(slot.Date);
        if (start > now) continue;
        if (end > now && !random.Chance(0.96)) continue; // ca đang diễn ra: có người chưa kịp chấm công
        if (random.Chance(rates.AbsentProbability) && end <= now) continue; // vắng: không có lượt chấm công

        var branch = person.Branch;
        var lateMinutes = random.Chance(rates.LateProbability) ? Math.Max(shift.LateGraceMinutes + 1, random.Exponential(rates.LateMeanMinutes)) : (random.Chance(0.10) ? random.Next(1, shift.LateGraceMinutes + 1) : 0);
        var checkIn = lateMinutes > 0 ? start.AddMinutes(lateMinutes).AddSeconds(random.Next(60)) : start.AddMinutes(-random.Next(0, 13)).AddSeconds(random.Next(60));
        if (checkIn >= end) checkIn = end.AddMinutes(-30);

        DateTime? checkOut = null;
        if (end <= now)
        {
            var early = random.Chance(rates.EarlyLeaveProbability);
            checkOut = early ? end.AddMinutes(-random.Next(shift.EarlyLeaveGraceMinutes + 3, 46)) : end.AddMinutes(random.Next(0, 13)).AddSeconds(random.Next(60));
            if (checkOut <= checkIn) checkOut = checkIn.AddMinutes(30);
            var recent = slot.Date >= today.AddDays(-14) && slot.Date < today;
            if (recent && random.Chance(0.008)) checkOut = null; // quên check-out (Task 6.1: Quản lý chỉnh giờ ra)
        }

        // Ba lớp xác minh: đa số đạt; vài lượt Wi-Fi không khớp → chờ duyệt (còn trong hàng duyệt nếu mới), cũ hơn thì đã được duyệt / từ chối
        var wifiOk = !(branches.First(x => x.Branch.Id == branch.Id).Bssids.Count > 0 && random.Chance(slot.Date >= today.AddDays(-12) ? 0.035 : 0.02));
        var status = AttendanceStatus.Valid;
        string? note = null;
        int? reviewedBy = null;
        DateTime? reviewedAt = null;
        string? reviewNote = null;
        if (!wifiOk)
        {
            note = "Wi-Fi đang kết nối không thuộc chi nhánh — chờ Quản lý duyệt.";
            if (slot.Date < today.AddDays(-10))
            {
                reviewedBy = managerOf[branch.Id];
                reviewedAt = slot.Date.AddDays(1).AddHours(9);
                var reject = random.Chance(0.3);
                status = reject ? AttendanceStatus.Invalid : AttendanceStatus.Valid;
                reviewNote = reject ? "Không xác nhận được nhân viên có mặt." : "Đã xác nhận với ca trưởng: điện thoại đổi mạng.";
            }
            else
            {
                status = AttendanceStatus.PendingApproval;
            }
        }

        var bssids = branches.First(x => x.Branch.Id == branch.Id).Bssids;
        var distance = random.NextDouble() * branch.Radius * 0.8 + 3;
        var angle = random.NextDouble() * Math.PI * 2;
        var lat = branch.Latitude + (decimal)(distance * Math.Cos(angle) / 111_320);
        var lng = branch.Longitude + (decimal)(distance * Math.Sin(angle) / (111_320 * Math.Cos((double)branch.Latitude * Math.PI / 180)));
        var bssid = bssids.Count == 0 ? null : wifiOk ? random.PickOne(bssids) : $"3C:84:6A:{random.Next(256):X2}:{random.Next(256):X2}:{random.Next(256):X2}";

        var log = new AttendanceLog
        {
            UserId = person.Id, BranchId = branch.Id, WorkDate = slot.Date, CheckinTime = checkIn, CheckinImageUrl = imageUrl,
            CheckinLat = Math.Round(lat, 8), CheckinLng = Math.Round(lng, 8), CheckinAccuracy = Math.Round(5 + random.NextDouble() * 20, 1),
            CheckinDistance = Math.Round(distance, 1), CheckinBssid = bssid, CheckinWifiMatched = wifiOk && bssids.Count > 0,
            CheckinFaceMatched = true, CheckinFaceScore = Math.Round(0.45 + random.NextDouble() * 0.4, 3), CheckinDeviceId = $"demo-device-{person.Id}",
            LateMinutes = ViolationPolicy.LateMinutes(shift, slot.Date, checkIn), Status = status, Note = note,
            ReviewedBy = reviewedBy, ReviewedAt = reviewedAt, ReviewNote = reviewNote
        };
        if (checkOut != null)
        {
            log.CheckoutTime = checkOut;
            log.CheckoutImageUrl = imageUrl;
            log.CheckoutLat = log.CheckinLat;
            log.CheckoutLng = log.CheckinLng;
            log.CheckoutAccuracy = log.CheckinAccuracy;
            log.CheckoutDistance = log.CheckinDistance;
            log.CheckoutBssid = log.CheckinBssid;
            log.CheckoutWifiMatched = log.CheckinWifiMatched;
            log.CheckoutFaceMatched = true;
            log.CheckoutFaceScore = Math.Round(0.45 + random.NextDouble() * 0.4, 3);
            log.CheckoutDeviceId = log.CheckinDeviceId;
            log.EarlyMinutes = ViolationPolicy.EarlyLeaveMinutes(shift, slot.Date, checkOut.Value);
            log.ActualWorkHours = AttendanceRules.WorkedHours(shift, slot.Date, checkIn, checkOut.Value);
        }

        slot.Schedule.Status = checkOut != null ? ScheduleStatus.Completed : ScheduleStatus.InProgress;
        logs.Add((slot, log));
    }

    // ---------------------------------------------------------------- 6. Ghi lịch + chấm công + quỹ phép + đơn nghỉ
    foreach (var slot in slots) slot.Schedule.UserId = slot.Person.Id;
    await db.InsertManyAsync("schedules", slots.Select(s => s.Schedule).ToList());
    var scheduleIds = (await db.QueryAsync<ScheduleKey>(
            "SELECT id AS Id, user_id AS UserId, shift_id AS ShiftId, work_date AS WorkDate FROM schedules WHERE user_id IN @Ids", new { Ids = workers.Select(w => w.Id).ToList() }))
        .ToDictionary(k => (k.UserId, k.ShiftId, k.WorkDate.Date), k => k.Id);
    foreach (var (slot, log) in logs) log.ScheduleId = scheduleIds[(slot.Person.Id, slot.Schedule.ShiftId, slot.Date)];
    await db.InsertManyAsync("attendance_logs", logs.Select(l => l.Log).ToList());

    var year = today.Year;
    await db.InsertManyAsync("leave_balances", people.Select(p => new LeaveBalance
    {
        UserId = p.Id, Year = year, TotalDays = 12, UsedDays = usedLeave.GetValueOrDefault(p.Id), RemainingDays = 12 - usedLeave.GetValueOrDefault(p.Id)
    }).ToList());
    await db.InsertManyAsync("leave_requests", leaves);

    // ---------------------------------------------------------------- 7. Đơn giải trình / OT / đổi ca (cần mã ca và mã chấm công đã có)
    var extra = new List<LeaveRequest>();
    if (explanationType != null)
    {
        var late = await db.QueryAsync<LateLog>(
            "SELECT id AS Id, user_id AS UserId, branch_id AS BranchId, work_date AS WorkDate FROM attendance_logs WHERE user_id IN @Ids AND late_minutes >= 15 AND work_date >= @From ORDER BY work_date DESC LIMIT 40",
            new { Ids = workers.Select(w => w.Id).ToList(), From = today.AddDays(-14) });
        foreach (var l in late.OrderBy(_ => random.Next()).Take(10))
        {
            extra.Add(new LeaveRequest
            {
                UserId = l.UserId, BranchId = l.BranchId, LeaveTypeId = explanationType.Id, StartDate = l.WorkDate.Date, EndDate = l.WorkDate.Date, AttendanceLogId = l.Id,
                Reason = random.PickOne(new[] { "Kẹt xe giờ cao điểm", "Xe hỏng giữa đường", "Đưa người nhà đi khám", "Mưa lớn, đường ngập" }), Status = RequestStatus.Pending
            });
        }
    }
    if (overtimeType != null)
    {
        foreach (var person in workers.Where(w => w.Trait is Trait.Reliable or Trait.Average).OrderBy(_ => random.Next()).Take(12))
        {
            var date = PickDate(person, windowStart.AddDays(14), today.AddDays(3));
            if (date == null) continue;
            var startAt = date.Value.AddHours(17);
            var past = date.Value < today;
            extra.Add(new LeaveRequest
            {
                UserId = person.Id, BranchId = person.Branch.Id, LeaveTypeId = overtimeType.Id, StartDate = date.Value, EndDate = date.Value, StartAt = startAt, EndAt = startAt.AddHours(2), DurationHours = 2,
                Reason = random.PickOne(new[] { "Kiểm kê cuối tháng", "Cửa hàng đông khách, xin ở lại hỗ trợ", "Hoàn tất bàn giao hàng nhập" }),
                Status = past ? RequestStatus.Approved : RequestStatus.Pending, ApprovedBy = past ? managerOf[person.Branch.Id] : null, ApprovedAt = past ? Min(date.Value.AddDays(-1).AddHours(15), now) : null
            });
        }
    }
    if (swapType != null)
    {
        // Người "xấu dần" xin đổi ca nhiều hơn: tín hiệu thứ tư của điểm nguy cơ nghỉ việc
        foreach (var person in workers.Where(w => w.Trait == Trait.Declining).Concat(workers.Where(w => w.Trait == Trait.Average).Take(2)))
        {
            var date = PickDate(person, today.AddDays(1), today.AddDays(9));
            if (date == null) continue;
            var mySlot = slots.First(s => s.Person == person && s.Date == date.Value.Date && !s.OnLeave);
            var colleague = workers.Where(w => w.Branch.Id == person.Branch.Id && w != person && !slots.Any(s => s.Person == w && s.Date == date.Value.Date)).OrderBy(_ => random.Next()).FirstOrDefault();
            if (colleague == null) continue;
            extra.Add(new LeaveRequest
            {
                UserId = person.Id, BranchId = person.Branch.Id, LeaveTypeId = swapType.Id, StartDate = date.Value, EndDate = date.Value, TargetUserId = colleague.Id,
                SourceScheduleId = scheduleIds[(person.Id, mySlot.Schedule.ShiftId, date.Value.Date)], PeerRespondedAt = now, PeerNote = "Đồng ý làm thay",
                Reason = "Có việc đột xuất, nhờ đồng nghiệp làm thay", Status = RequestStatus.Pending
            });
        }
    }
    await db.InsertManyAsync("leave_requests", extra);

    // Ngày gửi đơn = trước ngày áp dụng 2 ngày (CSDL tự gán created_at = lúc nạp dữ liệu nên chỉnh lại cho hợp lý). Đơn cho ngày sắp tới thì gửi
    // trong vài chục giờ qua — không bao giờ để ngày gửi nằm ở tương lai.
    await db.ExecAsync(
        "UPDATE leave_requests SET created_at = LEAST(TIMESTAMP(DATE_SUB(start_date, INTERVAL 2 DAY), '09:00:00'), DATE_SUB(NOW(), INTERVAL (id MOD 40) HOUR)) WHERE user_id IN @Ids",
        new { Ids = people.Select(p => p.Id).ToList() });

    // ---------------------------------------------------------------- Tóm tắt
    Console.WriteLine();
    Console.WriteLine($"Đã tạo: {people.Count} tài khoản ({workers.Count} nhân viên có lịch, {branches.Count} quản lý chi nhánh), {slots.Count} ca, {logs.Count} lượt chấm công, {leaves.Count + extra.Count} đơn.");
    Console.WriteLine($"Chi nhánh: {string.Join(", ", branches.Select(b => $"{b.Branch.BranchCode} ({people.Count(p => p.Branch.Id == b.Branch.Id && p.Works)} NV)"))}");
    Console.WriteLine($"Quản lý (mật khẩu chung): {string.Join(", ", people.Where(p => p.User.EmployeeCode.StartsWith("DMQ", StringComparison.Ordinal)).Select(p => p.User.Email))}");
    Console.WriteLine($"Nhân viên \"xấu dần\" (cho Task 7.4): {string.Join(", ", workers.Where(w => w.Trait == Trait.Declining).Select(w => $"{w.User.EmployeeCode} {w.User.FullName}"))}");
    Console.WriteLine($"Mật khẩu: {(isLocal && password == "Admin@123" ? "Admin@123 (CSDL local)" : "đúng giá trị bạn đã truyền vào --password")}");
    return 0;
});

return 0;

static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

static async Task<List<BranchInfo>> EnsureBranchesAsync(Db db)
{
    var definitions = new[]
    {
        ("CN-HN-01", "FlexiWork Cầu Giấy", "Tòa nhà FPT Cầu Giấy, Phố Duy Tân, Dịch Vọng Hậu, Cầu Giấy, Hà Nội", 21.031584m, 105.783115m, 50, "00:1A:2B:3C:4D:5E"),
        ("CN-HN-02", "FlexiWork Hoàn Kiếm", "Số 25 Lý Thường Kiệt, Phan Chu Trinh, Hoàn Kiếm, Hà Nội", 21.023412m, 105.852140m, 60, "AA:BB:CC:DD:EE:01"),
        ("CN-HN-03", "FlexiWork Đống Đa", "Số 12 Thái Hà, Trung Liệt, Đống Đa, Hà Nội", 21.013200m, 105.820300m, 60, "DE:AD:BE:EF:03:01"),
        ("CN-HCM-01", "FlexiWork Quận 1", "Số 45 Lê Lợi, Bến Nghé, Quận 1, Thành phố Hồ Chí Minh", 10.773100m, 106.700900m, 60, "DE:AD:BE:EF:04:01")
    };

    var result = new List<BranchInfo>();
    foreach (var (code, name, address, lat, lng, radius, bssid) in definitions)
    {
        var branch = (await db.QueryAsync<Branch>($"SELECT {EntityMap.For<Branch>().SelectColumns} FROM branches WHERE branch_code = @Code AND is_deleted = 0", new { Code = code })).FirstOrDefault();
        if (branch == null)
        {
            branch = new Branch { BranchCode = code, Name = name, Address = address, Latitude = lat, Longitude = lng, Radius = radius, Status = BranchStatus.Active, Phone = "0243" + code.GetHashCode() % 10_000_000 };
            branch.Phone = "02437" + Math.Abs(code.GetHashCode() % 100_000).ToString("00000");
            branch.Id = await db.InsertAsync<Branch, int>("branches", branch);
            await db.ExecAsync("INSERT INTO branch_wifis (branch_id, ssid, bssid, is_active) VALUES (@B, @S, @M, 1)", new { B = branch.Id, S = $"{name.Replace(" ", "_")}_Staff", M = bssid });
        }

        var bssids = await db.QueryAsync<string>("SELECT bssid FROM branch_wifis WHERE branch_id = @B AND is_active = 1 AND is_deleted = 0", new { B = branch.Id });
        result.Add(new BranchInfo(branch, bssids));
    }
    return result;
}

static async Task<List<Shift>> EnsureShiftsAsync(Db db)
{
    const decimal HalfDay = 0.5m; // ca 4 giờ = nửa công
    var definitions = new[] { ("DM-SANG", "Ca sáng (demo)", 8), ("DM-CHIEU", "Ca chiều (demo)", 13), ("DM-TOI", "Ca tối (demo)", 18) };
    var result = new List<Shift>();
    foreach (var (code, name, startHour) in definitions)
    {
        var shift = (await db.QueryAsync<Shift>($"SELECT {EntityMap.For<Shift>().SelectColumns} FROM shifts WHERE shift_code = @Code", new { Code = code })).FirstOrDefault();
        if (shift == null)
        {
            shift = new Shift
            {
                ShiftCode = code, Name = name, StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(startHour == 18 ? 22 : startHour + 4),
                BreakMinutes = 0, WorkUnits = HalfDay, IsNightShift = false, IsCrossDay = false, LateGraceMinutes = 5, EarlyLeaveGraceMinutes = 5
            };
            shift.Id = await db.InsertAsync<Shift, int>("shifts", shift);
        }
        else if (shift.WorkUnits != HalfDay)
        {
            // Bộ demo cũ tạo ca 4 giờ tính 1 công → toàn thời gian làm 2 ca/ngày bị tính 2 công/ngày. Ca nửa ngày là 0,5 công.
            await db.ExecAsync("UPDATE shifts SET work_units = @U WHERE id = @Id", new { U = HalfDay, shift.Id });
            shift.WorkUnits = HalfDay;
        }
        result.Add(shift);
    }
    return result;
}

static async Task<int> EnsurePositionAsync(Db db, string titleLike, string title, decimal hourly) =>
    await db.ScalarAsync<int?>("SELECT id FROM positions WHERE title LIKE @Like LIMIT 1", new { Like = titleLike })
    ?? await db.ScalarAsync<int>("INSERT INTO positions (title, base_salary_per_hour, description) VALUES (@T, @H, 'Dữ liệu demo'); SELECT LAST_INSERT_ID();", new { T = title, H = hourly });

namespace DemoDataSeeder
{
    internal sealed class Slot
    {
        public required Person Person { get; init; }
        public required DateTime Date { get; init; }
        public required int ShiftIndex { get; init; }
        public required Schedule Schedule { get; init; }
        public bool OnLeave { get; set; }
    }

    internal sealed record BranchInfo(Branch Branch, List<string> Bssids);
    internal sealed record IdName(int Id, string Name);
    internal sealed record LeaveTypeRow(int Id, string Category, bool DeductsBalance, bool RequiresAttachment);
    internal sealed record ScheduleKey(long Id, int UserId, int ShiftId, DateTime WorkDate);
    internal sealed record LateLog(long Id, int UserId, int BranchId, DateTime WorkDate);
}
