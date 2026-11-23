using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Settings;
using FW.Application.Features.Attendance;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.IntegrationTests.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FW.IntegrationTests.Attendance;

/// <summary>
/// Engine chấm công 3 lớp (Task 3.2) trên MySQL thật, nhận diện khuôn mặt và kho ảnh giả lập.
/// Mỗi test tạo nhân viên + ca + lịch riêng ở một ngày tương lai riêng để không ảnh hưởng nhau.
/// Chi nhánh 1 (seed): tâm 21.031584, 105.783115, bán kính 50 m, Wi-Fi 00:1A:2B:3C:4D:5E và 00:1A:2B:3C:4D:5F.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AttendanceServiceTests
{
    private const int Branch1 = 1;
    private const double BranchLat = 21.031584, BranchLng = 105.783115;
    private const string OfficeBssid = "00:1A:2B:3C:4D:5E";

    private readonly DatabaseFixture _db;

    public AttendanceServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ test doubles

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTime Now { get; set; }
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public DateTime UtcNow => Now.AddHours(-7);
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int userId) => UserId = userId;
        public int? UserId { get; }
        public int? BranchId => Branch1;
        public IReadOnlyList<string> Roles => new[] { RoleNames.Employee };
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == RoleNames.Employee;
    }

    /// <summary>Trả về độ tương đồng cấu hình sẵn (ngưỡng 0,35 như cấu hình thật).</summary>
    private sealed class StubFace : IFaceRecognitionService
    {
        public double Score { get; set; } = 0.72;
        public string? Error { get; set; }
        public int Calls { get; private set; }

        public Task<FaceExtractResult> ExtractEmbeddingAsync(Stream image, string fileName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FaceVerifyResult> VerifyAsync(Stream image, string fileName, IReadOnlyList<float> targetEmbedding, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Error != null
                ? new FaceVerifyResult(false, Error, false, 0, 0.35)
                : new FaceVerifyResult(true, "ok", Score >= 0.35, Score, 0.35));
        }
    }

    /// <summary>Kho nhật ký lượt chấm công luôn lỗi — chứng minh lỗi ghi nhật ký không làm hỏng lượt chấm công.</summary>
    private sealed class BrokenAttempts : AttendanceAttemptRepository
    {
        public BrokenAttempts(DbSession session) : base(session) { }

        public override Task<long> AddAsync(AttendanceAttempt entity, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("kho nhật ký hỏng");
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        public List<string> Uploaded { get; } = new();
        public List<string> Deleted { get; } = new();

        public Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            var id = $"{folder}/{Guid.NewGuid():N}";
            Uploaded.Add(id);
            return Task.FromResult(new FileUploadResult($"https://img.test/{id}.jpg", id));
        }

        public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(publicId);
            return Task.FromResult(true);
        }
    }

    private sealed class Ctx
    {
        public required DbSession Session { get; init; }
        public required int UserId { get; init; }
        public required Schedule Schedule { get; init; }
        public required Shift Shift { get; init; }
        public TestClock Clock { get; } = new();
        public StubFace Face { get; } = new();
        public RecordingNotifier Notes { get; } = new();
        public MemoryStorage Storage { get; } = new();

        public AttendanceService Service(int? userId = null, IAttendanceAttemptRepository? attempts = null)
        {
            var s = Session;
            return new AttendanceService(
                new AttendanceLogRepository(s), attempts ?? new AttendanceAttemptRepository(s), new ScheduleRepository(s), new ShiftRepository(s), new BranchRepository(s),
                new BranchWifiRepository(s), new FaceProfileRepository(s), new UserRepository(s), Notes, new ShiftTaskAssignmentRepository(s),
                Face, Storage, s, new TestUser(userId ?? UserId), Clock,
                Options.Create(new AttendanceSettings()), NullLogger<AttendanceService>.Instance);
        }

        public DateTime At(int hour, int minute = 0) => Schedule.WorkDate.AddHours(hour).AddMinutes(minute);
    }

    // ------------------------------------------------------------------ dữ liệu

    private static async Task<int> NewEmployeeAsync(DbSession s, bool withFace = true)
    {
        var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = "Nhân viên chấm công", BranchId = Branch1
        };
        await new UserRepository(s).AddAsync(user);
        if (withFace)
        {
            var embedding = Enumerable.Range(0, FaceProfile.EmbeddingSize).Select(i => (float)Math.Sin(i)).ToArray();
            await new FaceProfileRepository(s).UpsertAsync(new FaceProfile { UserId = user.Id, FaceEncoding = embedding, SampleImageUrl = "https://img.test/face.jpg" });
        }
        return user.Id;
    }

    /// <summary>Nhân viên mới + ca 08:00–17:00 (nghỉ 60', cho phép muộn/sớm 5') đã công bố vào ngày <paramref name="workDate"/>.</summary>
    private async Task<Ctx> SetupAsync(DateTime workDate, bool withFace = true, int startHour = 8, int endHour = 17)
    {
        var s = _db.CreateSession();
        var userId = await NewEmployeeAsync(s, withFace);
        var shift = new Shift
        {
            ShiftCode = $"AS{Guid.NewGuid():N}"[..14], Name = "Ca hành chính", BranchId = Branch1,
            StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour), BreakMinutes = 60,
            IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour))
        };
        await new ShiftRepository(s).AddAsync(shift);
        var schedule = new Schedule
        {
            UserId = userId, ShiftId = shift.Id, BranchId = Branch1, WorkDate = workDate, IsPublished = true, PublishedAt = workDate.AddDays(-2)
        };
        await new ScheduleRepository(s).AddAsync(schedule);
        return new Ctx { Session = s, UserId = userId, Schedule = schedule, Shift = shift };
    }

    /// <summary>Đứng cách tâm chi nhánh khoảng <paramref name="metersNorth"/> mét về phía bắc.</summary>
    private static AttendanceCaptureRequest AtBranch(double metersNorth = 10, string? bssid = OfficeBssid, string? deviceId = null) => new()
    {
        Latitude = BranchLat + metersNorth / 111_195.0,
        Longitude = BranchLng,
        Accuracy = 8,
        Bssid = bssid,
        DeviceId = deviceId
    };

    private static Task<AttendanceResultDto> CheckIn(AttendanceService service, AttendanceCaptureRequest request) =>
        service.CheckInAsync(request, new MemoryStream(new byte[] { 1, 2, 3 }), "selfie.jpg", "image/jpeg", 3);

    private static Task<AttendanceResultDto> CheckOut(AttendanceService service, AttendanceCaptureRequest request) =>
        service.CheckOutAsync(request, new MemoryStream(new byte[] { 1, 2, 3 }), "selfie.jpg", "image/jpeg", 3);

    // ------------------------------------------------------------------ tests

    [SkippableFact]
    public async Task Full_shift_check_in_then_check_out_records_evidence_and_times()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 4, 5));
        await using var _ = ctx.Session;
        var service = ctx.Service();
        await new ShiftTaskRepository(ctx.Session).AddAsync(new ShiftTask
        {
            ShiftId = ctx.Shift.Id, BranchId = Branch1, Title = "Mở két đầu ca", Phase = TaskPhase.Start, IsMandatory = true
        });

        // 06:30: chưa mở khung check-in (mở lúc 07:00)
        ctx.Clock.Now = ctx.At(6, 30);
        var early = await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch()));
        Assert.Contains("07:00", early.Message);
        var before = await service.GetTodayAsync();
        Assert.Equal(AttendanceActions.None, before.NextAction);
        Assert.True(before.FaceRegistered);
        Assert.Single(before.Shifts);

        // 07:30: ứng dụng được báo cần check-in, kèm chi nhánh để tự đo khoảng cách
        ctx.Clock.Now = ctx.At(7, 30);
        var today = await service.GetTodayAsync();
        Assert.Equal(AttendanceActions.CheckIn, today.NextAction);
        Assert.Equal(ctx.Schedule.Id, today.ActionScheduleId);
        Assert.Equal(50, today.Branch!.Radius);
        Assert.True(today.Branch.RequiresWifi);

        // 08:07:30 check-in: đủ 3 lớp, muộn 7 phút (vượt 5 phút cho phép)
        ctx.Clock.Now = ctx.At(8, 7).AddSeconds(30);
        var checkIn = await CheckIn(service, AtBranch(metersNorth: 20, bssid: "00:1a:2b:3c:4d:5e", deviceId: "dev-full"));
        Assert.Equal(AttendanceStatus.Valid, checkIn.Status);
        Assert.Equal(7, checkIn.LateMinutes);
        Assert.Contains("đi muộn 7 phút", checkIn.Message);
        Assert.Contains("1 việc trong checklist", checkIn.Message);
        var assigned = Assert.Single(ctx.Notes.To(ctx.UserId)); // được giao checklist → báo chính mình
        Assert.Equal((NotificationType.TaskAssigned, ctx.Schedule.Id.ToString()), (assigned.Type, assigned.Reference));
        Assert.Empty(ctx.Notes.ToReviewers); // đạt đủ 3 lớp: không cần ai duyệt
        Assert.Single(await new ShiftTaskAssignmentRepository(ctx.Session).GetDetailsByScheduleIdsAsync(new[] { ctx.Schedule.Id }));
        Assert.InRange(checkIn.Gps.DistanceMeters, 19.5, 20.5);
        Assert.Equal("matched", checkIn.Wifi.Status);
        Assert.Equal(0.72, checkIn.Face.Score);
        Assert.Empty(checkIn.Warnings);

        var logs = new AttendanceLogRepository(ctx.Session);
        var log = await logs.GetByScheduleIdAsync(ctx.Schedule.Id);
        Assert.NotNull(log);
        Assert.Equal(OfficeBssid, log!.CheckinBssid);
        Assert.True(log.CheckinWifiMatched && log.CheckinFaceMatched);
        Assert.Equal("dev-full", log.CheckinDeviceId);
        Assert.StartsWith("https://img.test/attendance/", log.CheckinImageUrl);
        Assert.Equal(ScheduleStatus.InProgress, (await new ScheduleRepository(ctx.Session).GetByIdAsync(ctx.Schedule.Id))!.Status);

        // Check-in lại khi đang trong ca bị chặn; màn hình chuyển sang check-out
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch()));
        Assert.Equal(AttendanceActions.CheckOut, (await service.GetTodayAsync()).NextAction);

        // 16:50 check-out: về sớm 10 phút; giờ làm = (08:07:30 → 16:50) × 480/540
        ctx.Clock.Now = ctx.At(16, 50);
        var checkOut = await CheckOut(service, AtBranch(deviceId: "dev-full"));
        Assert.Equal(AttendanceStatus.Valid, checkOut.Status);
        Assert.Equal(10, checkOut.EarlyMinutes);
        Assert.Equal(7.74m, checkOut.ActualWorkHours);
        Assert.Contains("về sớm 10 phút", checkOut.Message);
        Assert.Contains("Còn 1 việc bắt buộc", checkOut.Message);
        Assert.Empty(ctx.Notes.ToReviewers);
        Assert.Equal(AttendanceStatus.Valid, checkOut.Status); // việc chưa xong không làm lượt chấm công phải chờ duyệt

        log = await logs.GetByIdAsync(checkIn.LogId);
        Assert.Equal(ctx.At(16, 50), log!.CheckoutTime);
        Assert.Equal(7, log.LateMinutes);
        Assert.Equal(ScheduleStatus.Completed, (await new ScheduleRepository(ctx.Session).GetByIdAsync(ctx.Schedule.Id))!.Status);
        Assert.Equal(2, ctx.Storage.Uploaded.Count);
        Assert.Empty(ctx.Storage.Deleted);

        // Xong ca: không còn việc gì, check-out lần nữa bị chặn
        var after = await service.GetTodayAsync();
        Assert.Equal(AttendanceActions.None, after.NextAction);
        Assert.Equal(AttendanceStatus.Valid, Assert.Single(after.Shifts).Log!.Status);
        await Assert.ThrowsAsync<BadRequestException>(() => CheckOut(service, AtBranch()));
    }

    [SkippableFact]
    public async Task Failed_mandatory_layers_reject_without_saving_anything()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 4, 6));
        await using var _ = ctx.Session;
        var service = ctx.Service();
        ctx.Clock.Now = ctx.At(7, 55);

        // Fake GPS
        var mocked = AtBranch();
        mocked.IsMocked = true;
        Assert.Contains("giả lập", (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, mocked))).Message);

        // Thiếu tọa độ / sai số GPS quá lớn
        Assert.Contains("vị trí GPS", (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, new AttendanceCaptureRequest()))).Message);
        var weak = AtBranch();
        weak.Accuracy = 150;
        Assert.Contains("GPS yếu", (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, weak))).Message);

        // Ngoài bán kính 50 m (đứng cách ~80 m) — không tốn công chạy nhận diện khuôn mặt
        var far = await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch(metersNorth: 80)));
        Assert.Contains("ngoài phạm vi chấm công 50 m", far.Message);
        Assert.Equal(0, ctx.Face.Calls);

        // Khuôn mặt không khớp / không thấy khuôn mặt
        ctx.Face.Score = 0.21;
        Assert.Contains("không khớp", (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch()))).Message);
        ctx.Face.Error = "Không tìm thấy khuôn mặt trong ảnh.";
        Assert.Equal(ctx.Face.Error, (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch()))).Message);

        Assert.Null(await new AttendanceLogRepository(ctx.Session).GetByScheduleIdAsync(ctx.Schedule.Id));
        Assert.Empty(ctx.Storage.Uploaded);

        // Đạt lại cả 3 lớp thì check-in được bình thường
        ctx.Face.Error = null;
        ctx.Face.Score = 0.65;
        Assert.Equal(AttendanceStatus.Valid, (await CheckIn(service, AtBranch(metersNorth: 49))).Status);
    }

    [SkippableFact]
    public async Task Employee_without_a_usable_face_profile_cannot_check_in()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 4, 7), withFace: false);
        await using var _ = ctx.Session;
        ctx.Clock.Now = ctx.At(8);

        Assert.Contains("chưa đăng ký khuôn mặt", (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(ctx.Service(), AtBranch()))).Message);
        Assert.False((await ctx.Service().GetTodayAsync()).FaceRegistered);

        // Nhân viên seed EMP003 có vector mẫu cũ (12 chiều) → phải đăng ký lại
        Assert.Contains("đã cũ", (await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(ctx.Service(userId: 3), AtBranch()))).Message);
    }

    [SkippableFact]
    public async Task Wifi_or_shared_device_problems_are_recorded_as_pending_approval()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var workDate = new DateTime(2032, 4, 8);
        var first = await SetupAsync(workDate);
        await using var s1 = first.Session;
        var second = await SetupAsync(workDate);
        await using var s2 = second.Session;
        first.Clock.Now = second.Clock.Now = first.At(7, 58);

        // Không đọc được Wi-Fi (VD iPhone trong Expo Go) → vẫn ghi nhận, chờ duyệt
        var noWifi = await CheckIn(first.Service(), AtBranch(bssid: null, deviceId: "dev-shared"));
        Assert.Equal(AttendanceStatus.PendingApproval, noWifi.Status);
        var review = Assert.Single(first.Notes.ToReviewers); // lượt chờ duyệt → báo người duyệt của chi nhánh
        Assert.Equal((Branch1, first.UserId, NotificationType.Attendance, noWifi.LogId.ToString()), (review.BranchId, review.Requester, review.Type, review.Reference));
        Assert.Contains("check-in", review.Content);
        Assert.Equal("no_wifi_info", noWifi.Wifi.Status);
        Assert.Contains("chờ Quản lý duyệt", noWifi.Message);
        Assert.Equal(0, noWifi.LateMinutes);

        // Người thứ hai chấm công trên cùng điện thoại → chờ duyệt, ghi rõ mã nhân viên trước
        var shared = await CheckIn(second.Service(), AtBranch(deviceId: "dev-shared"));
        Assert.Equal(AttendanceStatus.PendingApproval, shared.Status);
        Assert.Equal("matched", shared.Wifi.Status);
        var firstCode = (await new UserRepository(s1).GetByIdAsync(first.UserId))!.EmployeeCode;
        Assert.Contains(firstCode, Assert.Single(shared.Warnings));

        // Check-out bằng Wi-Fi lạ: bản ghi vẫn chờ duyệt, ghi chú cộng dồn
        first.Clock.Now = first.At(17, 5);
        var checkOut = await CheckOut(first.Service(), AtBranch(bssid: "AA:BB:CC:DD:EE:FF"));
        Assert.Equal(AttendanceStatus.PendingApproval, checkOut.Status);
        Assert.Equal("not_matched", checkOut.Wifi.Status);
        Assert.Equal(8.00m, checkOut.ActualWorkHours);
        var log = await new AttendanceLogRepository(s1).GetByIdAsync(noWifi.LogId);
        Assert.Contains("lúc vào", log!.Note);
        Assert.Contains("AA:BB:CC:DD:EE:FF", log.Note);
        Assert.Equal("AA:BB:CC:DD:EE:FF", log.CheckoutBssid);
        Assert.False(log.CheckoutWifiMatched);
    }

    [SkippableFact]
    public async Task Check_out_with_unknown_wifi_sends_a_valid_check_in_back_for_review()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2036, 6, 3));
        await using var _ = ctx.Session;
        var service = ctx.Service();

        ctx.Clock.Now = ctx.At(8);
        Assert.Equal(AttendanceStatus.Valid, (await CheckIn(service, AtBranch())).Status);
        Assert.Empty(ctx.Notes.ToReviewers);

        ctx.Clock.Now = ctx.At(17);
        var checkOut = await CheckOut(service, AtBranch(bssid: "AA:BB:CC:DD:EE:FF"));
        Assert.Equal(AttendanceStatus.PendingApproval, checkOut.Status);
        var review = Assert.Single(ctx.Notes.ToReviewers); // chỉ báo khi lượt ra khiến bản ghi chuyển sang chờ duyệt
        Assert.Contains("check-out", review.Content);
        Assert.Equal(checkOut.LogId.ToString(), review.Reference);
    }

    [SkippableFact]
    public async Task Check_out_after_the_deadline_is_refused()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 4, 9));
        await using var _ = ctx.Session;
        var service = ctx.Service();

        ctx.Clock.Now = ctx.At(8);
        await CheckIn(service, AtBranch());

        // Hạn check-out = 17:00 + 240 phút = 21:00
        ctx.Clock.Now = ctx.At(21, 1);
        Assert.Contains("quá hạn check-out", (await Assert.ThrowsAsync<BadRequestException>(() => CheckOut(service, AtBranch()))).Message);
        Assert.Equal(AttendanceActions.None, (await service.GetTodayAsync()).NextAction);
    }

    [SkippableFact]
    public async Task Overnight_shift_is_checked_out_the_next_morning()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 4, 10), startHour: 22, endHour: 6);
        await using var _ = ctx.Session;
        var service = ctx.Service();

        ctx.Clock.Now = ctx.At(21, 45);
        var checkIn = await CheckIn(service, AtBranch());
        Assert.Equal(ctx.Schedule.WorkDate, (await new AttendanceLogRepository(ctx.Session).GetByIdAsync(checkIn.LogId))!.WorkDate);

        // Sáng hôm sau: ca đêm qua vẫn hiện trên màn hình, việc cần làm là check-out
        ctx.Clock.Now = ctx.At(30, 2); // 06:02 ngày hôm sau
        var today = await service.GetTodayAsync();
        Assert.Equal(AttendanceActions.CheckOut, today.NextAction);
        Assert.Contains(today.Shifts, s => s.ScheduleId == ctx.Schedule.Id);

        var checkOut = await CheckOut(service, AtBranch());
        Assert.Equal(0, checkOut.EarlyMinutes);
        Assert.Equal(7.00m, checkOut.ActualWorkHours); // 8 giờ có mặt − 60' nghỉ
    }

    private static async Task<List<AttendanceAttempt>> AttemptsOfAsync(Ctx ctx) =>
        (await new AttendanceAttemptRepository(ctx.Session).GetByPeriodAsync(ctx.Schedule.WorkDate, ctx.Schedule.WorkDate.AddDays(2)))
        .Where(a => a.UserId == ctx.UserId).ToList();

    [SkippableFact]
    public async Task Every_attempt_is_recorded_with_the_layer_that_decided_it()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 5, 3));
        await using var _ = ctx.Session;
        var service = ctx.Service();

        // 06:30 chưa tới khung check-in → chặn ở lớp thời gian, chưa biết chi nhánh
        ctx.Clock.Now = ctx.At(6, 30);
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch()));

        ctx.Clock.Now = ctx.At(7, 55);
        var mocked = AtBranch();
        mocked.IsMocked = true;
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, mocked));
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, new AttendanceCaptureRequest()));
        var weak = AtBranch();
        weak.Accuracy = 150;
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, weak));
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch(metersNorth: 80)));
        ctx.Face.Score = 0.21;
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch()));

        // Sai Wi-Fi nhưng đạt GPS + khuôn mặt → được ghi nhận, chờ duyệt
        ctx.Face.Score = 0.66;
        ctx.Clock.Now = ctx.At(8, 1);
        var pending = await CheckIn(service, AtBranch(bssid: "AA:BB:CC:11:22:33", deviceId: "dev-attempt"));
        Assert.Equal(AttendanceStatus.PendingApproval, pending.Status);

        var attempts = await AttemptsOfAsync(ctx);
        Assert.Equal(7, attempts.Count);
        Assert.All(attempts, a => Assert.Equal(AttendanceActions.CheckIn, a.Action));

        var time = attempts[0];
        Assert.Equal((AttemptOutcome.Rejected, AttemptStage.Time), (time.Outcome, time.Stage));
        Assert.Null(time.BranchId);
        Assert.Equal(ctx.At(6, 30), time.AttemptedAt);

        var fake = attempts[1];
        Assert.Equal((AttemptOutcome.Rejected, AttemptStage.Validate), (fake.Outcome, fake.Stage));
        Assert.True(fake.IsMocked);
        Assert.Contains("giả lập", fake.Message);

        var noGps = attempts[2];
        Assert.Equal(AttemptStage.Validate, noGps.Stage);
        Assert.Null(noGps.Latitude);

        var weakGps = attempts[3];
        Assert.Equal((AttemptStage.Gps, 150d), (weakGps.Stage, weakGps.Accuracy));
        Assert.Equal(Branch1, weakGps.BranchId);
        Assert.Null(weakGps.Distance);

        var far = attempts[4];
        Assert.Equal((AttemptOutcome.Rejected, AttemptStage.Gps), (far.Outcome, far.Stage));
        Assert.InRange(far.Distance!.Value, 79, 81);
        Assert.Null(far.FaceScore);

        var wrongFace = attempts[5];
        Assert.Equal((AttemptOutcome.Rejected, AttemptStage.Face), (wrongFace.Outcome, wrongFace.Stage));
        Assert.Equal(0.21, wrongFace.FaceScore);
        Assert.Equal(0.35, wrongFace.FaceThreshold);
        Assert.Equal(ctx.Schedule.Id, wrongFace.ScheduleId);

        var saved = attempts[6];
        Assert.Equal((AttemptOutcome.PendingApproval, AttemptStage.Save), (saved.Outcome, saved.Stage));
        Assert.Equal("not_matched", saved.WifiStatus);
        Assert.Equal("AA:BB:CC:11:22:33", saved.Bssid, ignoreCase: true);
        Assert.Equal(0.66, saved.FaceScore);
        Assert.Equal("dev-attempt", saved.DeviceId);
        Assert.True(saved.DurationMs >= 0);

        // Lượt check-out cũng được ghi
        ctx.Clock.Now = ctx.At(17, 0);
        await CheckOut(service, AtBranch());
        var last = (await AttemptsOfAsync(ctx)).Last();
        Assert.Equal((AttendanceActions.CheckOut, AttemptOutcome.PendingApproval, "matched"), (last.Action, last.Outcome, last.WifiStatus)); // bản ghi vẫn chờ duyệt vì lượt vào sai Wi-Fi
    }

    [SkippableFact]
    public async Task A_failing_attempt_log_never_blocks_check_in()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        var ctx = await SetupAsync(new DateTime(2032, 5, 4));
        await using var _ = ctx.Session;
        var service = ctx.Service(attempts: new BrokenAttempts(ctx.Session));

        ctx.Clock.Now = ctx.At(7, 55);
        await Assert.ThrowsAsync<BadRequestException>(() => CheckIn(service, AtBranch(metersNorth: 80))); // vẫn trả đúng lỗi nghiệp vụ
        var ok = await CheckIn(service, AtBranch());
        Assert.Equal(AttendanceStatus.Valid, ok.Status);
        Assert.Empty(await AttemptsOfAsync(ctx));
    }
}
