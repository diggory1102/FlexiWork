using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Settings;
using FW.Application.Features.Notifications;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Application.Features.Attendance;

public interface IAttendanceService
{
    /// <summary>Việc cần làm tiếp theo và các ca trong ngày của người đang đăng nhập.</summary>
    Task<AttendanceTodayDto> GetTodayAsync(CancellationToken cancellationToken = default);

    Task<AttendanceResultDto> CheckInAsync(AttendanceCaptureRequest request, Stream image, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default);

    Task<AttendanceResultDto> CheckOutAsync(AttendanceCaptureRequest request, Stream image, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Engine chấm công 3 lớp (UC 4.1, 4.2). Thứ tự kiểm tra từ rẻ tới đắt, dừng ở lớp bắt buộc đầu tiên không đạt:
/// <list type="number">
/// <item>Thời gian: ca đã công bố, đang trong khung check-in (hoặc còn hạn check-out).</item>
/// <item>Lớp 1 — GPS (bắt buộc): không giả lập vị trí, sai số đủ nhỏ, khoảng cách Haversine ≤ bán kính chi nhánh.</item>
/// <item>Lớp 3 — khuôn mặt (bắt buộc): ảnh selfie khớp khuôn mặt mẫu (cosine ≥ ngưỡng).</item>
/// <item>Lớp 2 — Wi-Fi (không bắt buộc): BSSID không khớp hoặc không đọc được → vẫn ghi nhận nhưng <c>pending_approval</c>
/// chờ Quản lý / HR duyệt. Cùng một thiết bị đã chấm công cho người khác trong ngày cũng chuyển chờ duyệt.</item>
/// </list>
/// Lượt bị từ chối không lưu bản ghi (chỉ ghi log hệ thống); lượt được ghi nhận lưu đủ minh chứng của cả 3 lớp.
/// </summary>
public class AttendanceService : IAttendanceService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public static readonly IReadOnlyList<string> AllowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };
    public const string StorageFolder = "attendance";
    private const int NoteMaxLength = 255;
    private const int DeviceIdMaxLength = 100;

    private readonly IAttendanceLogRepository _logs;
    private readonly IAttendanceAttemptRepository _attempts;
    private readonly IScheduleRepository _schedules;
    private readonly IShiftRepository _shifts;
    private readonly IBranchRepository _branches;
    private readonly IBranchWifiRepository _wifis;
    private readonly IFaceProfileRepository _faces;
    private readonly IUserRepository _users;
    private readonly INotifier _notifier;
    private readonly IShiftTaskAssignmentRepository _taskAssignments;
    private readonly IFaceRecognitionService _recognition;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly AttendanceSettings _settings;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IAttendanceLogRepository logs,
        IAttendanceAttemptRepository attempts,
        IScheduleRepository schedules,
        IShiftRepository shifts,
        IBranchRepository branches,
        IBranchWifiRepository wifis,
        IFaceProfileRepository faces,
        IUserRepository users,
        INotifier notifier,
        IShiftTaskAssignmentRepository taskAssignments,
        IFaceRecognitionService recognition,
        IFileStorageService storage,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IOptions<AttendanceSettings> settings,
        ILogger<AttendanceService> logger)
    {
        _logs = logs;
        _attempts = attempts;
        _schedules = schedules;
        _shifts = shifts;
        _branches = branches;
        _wifis = wifis;
        _faces = faces;
        _users = users;
        _notifier = notifier;
        _taskAssignments = taskAssignments;
        _recognition = recognition;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Màn hình chấm công
    // ------------------------------------------------------------------

    public async Task<AttendanceTodayDto> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        var now = _clock.Now;
        var faceRegistered = await _faces.GetByUserIdAsync(userId, cancellationToken) != null;

        // Ca hôm nay + ca hôm qua (ca qua đêm có thể chưa kết thúc / còn hạn check-out)
        var items = (await LoadScheduledShiftsAsync(userId, now.Date.AddDays(-1), now.Date, cancellationToken))
            .Where(i => i.Schedule.IsPublished && (i.Schedule.WorkDate == now.Date || CheckOutDeadline(i) > now))
            .ToList();
        var logs = (await _logs.GetByScheduleIdsAsync(items.Select(i => i.Schedule.Id), cancellationToken))
            .Where(l => l.ScheduleId.HasValue)
            .ToDictionary(l => l.ScheduleId!.Value);

        var open = await FindOpenAsync(userId, cancellationToken);
        var pick = AttendanceRules.PickCheckIn(items, now, _settings.EarlyCheckInMinutes);

        string action;
        ScheduledShift? target;
        string message;
        if (open != null && (now < open.Item.EndAt || pick == null) && now <= CheckOutDeadline(open.Item))
        {
            action = AttendanceActions.CheckOut;
            target = open.Item;
            message = $"Bạn đang trong {Label(open.Item.Shift)} (check-in lúc {open.Log.CheckinTime:HH:mm}).";
        }
        else if (pick != null)
        {
            action = AttendanceActions.CheckIn;
            target = pick;
            message = now < pick.StartAt
                ? $"{Sentence(pick.Shift)} bắt đầu lúc {pick.StartAt:HH:mm}."
                : $"{Sentence(pick.Shift)} đã bắt đầu lúc {pick.StartAt:HH:mm}.";
        }
        else
        {
            action = AttendanceActions.None;
            target = null;
            message = NoCheckInMessage(items, now);
        }

        AttendanceBranchDto? branch = null;
        if (target != null)
        {
            var b = await _branches.GetByIdAsync(target.Schedule.BranchId, cancellationToken);
            if (b != null)
            {
                var hasWifi = (await _wifis.GetByBranchIdAsync(b.Id, activeOnly: true, cancellationToken)).Count > 0;
                branch = new AttendanceBranchDto(b.Id, b.Name, b.Latitude, b.Longitude, b.Radius, hasWifi);
            }
        }

        var shifts = items
            .Where(i => i.Schedule.Status != ScheduleStatus.Cancelled)
            .OrderBy(i => i.StartAt)
            .Select(i => new AttendanceShiftDto(
                i.Schedule.Id, i.Schedule.WorkDate, i.Shift.ShiftCode, i.Shift.Name, i.StartAt, i.EndAt,
                AttendanceRules.CheckInOpensAt(i, _settings.EarlyCheckInMinutes), CheckOutDeadline(i), i.Schedule.Status,
                logs.TryGetValue(i.Schedule.Id, out var log) ? AttendanceLogSummaryDto.From(log) : null))
            .ToList();

        return new AttendanceTodayDto(now, faceRegistered, action, target?.Schedule.Id, message, branch, shifts);
    }

    // ------------------------------------------------------------------
    // Check-in
    // ------------------------------------------------------------------

    public Task<AttendanceResultDto> CheckInAsync(AttendanceCaptureRequest request, Stream image, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default) =>
        RunAndRecordAsync(AttendanceActions.CheckIn, request, trace => CheckInCoreAsync(request, image, fileName, contentType, length, trace, cancellationToken));

    private async Task<AttendanceResultDto> CheckInCoreAsync(AttendanceCaptureRequest request, Stream image, string fileName, string contentType, long length,
        AttemptTrace trace, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var now = _clock.Now;
        ValidateCapture(request, contentType, length);
        trace.Stage = AttemptStage.Face;
        var profile = await GetFaceProfileAsync(userId, cancellationToken);

        trace.Stage = AttemptStage.Time;
        var open = await FindOpenAsync(userId, cancellationToken);
        if (open != null && now < open.Item.EndAt)
        {
            throw new BadRequestException(
                $"Bạn đang trong {Label(open.Item.Shift)} (check-in lúc {open.Log.CheckinTime:HH:mm}). Hãy check-out ca này trước.");
        }

        var items = await LoadScheduledShiftsAsync(userId, now.Date.AddDays(-1), now.Date, cancellationToken);
        var target = AttendanceRules.PickCheckIn(items, now, _settings.EarlyCheckInMinutes)
                     ?? throw new BadRequestException(NoCheckInMessage(items, now));

        trace.ScheduleId = target.Schedule.Id;
        trace.BranchId = target.Schedule.BranchId;
        var branch = await GetBranchAsync(target.Schedule.BranchId, cancellationToken);
        trace.Stage = AttemptStage.Gps;
        var gps = CheckGps(branch, request, userId, AttendanceActions.CheckIn, trace);

        trace.Stage = AttemptStage.Face;
        await using var photo = await BufferAsync(image, cancellationToken);
        var face = await CheckFaceAsync(photo, fileName, profile, userId, AttendanceActions.CheckIn, trace, cancellationToken);
        trace.Stage = AttemptStage.Wifi;
        var wifi = await CheckWifiAsync(branch, request, trace, cancellationToken);
        trace.Stage = AttemptStage.Save;

        var warnings = new List<string>();
        if (!wifi.IsPassed) warnings.Add(WifiWarning(wifi));
        var deviceId = NormalizeDeviceId(request.DeviceId);
        if (deviceId != null)
        {
            var others = await _logs.GetOtherUsersOnDeviceAsync(deviceId, target.Schedule.WorkDate, userId, cancellationToken);
            if (others.Count > 0)
            {
                var codes = (await _users.GetByIdsAsync(others, cancellationToken)).Select(u => u.EmployeeCode).OrderBy(c => c);
                warnings.Add($"Thiết bị này đã được dùng để chấm công cho nhân viên khác trong ngày ({string.Join(", ", codes)}).");
            }
        }

        var log = new AttendanceLog
        {
            ScheduleId = target.Schedule.Id,
            UserId = userId,
            BranchId = branch.Id,
            WorkDate = target.Schedule.WorkDate,
            CheckinTime = now,
            CheckinLat = ToCoordinate(request.Latitude!.Value),
            CheckinLng = ToCoordinate(request.Longitude!.Value),
            CheckinAccuracy = request.Accuracy,
            CheckinDistance = Math.Round(gps.DistanceMeters, 2),
            CheckinBssid = wifi.Bssid,
            CheckinWifiMatched = wifi.Status == WifiCheckStatus.Matched,
            CheckinFaceMatched = true,
            CheckinFaceScore = face.Score,
            CheckinDeviceId = deviceId,
            LateMinutes = ViolationPolicy.LateMinutes(target.Shift, target.Schedule.WorkDate, now),
            Status = warnings.Count > 0 ? AttendanceStatus.PendingApproval : AttendanceStatus.Valid,
            Note = JoinNote(null, warnings)
        };

        var employee = await _users.GetByIdAsync(userId, cancellationToken);
        var positionId = employee?.PositionId;
        var uploaded = await UploadAsync(photo, fileName, cancellationToken);
        log.CheckinImageUrl = uploaded.Url;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _logs.AddAsync(log, ct);
                target.Schedule.Status = ScheduleStatus.InProgress;
                await _schedules.UpdateAsync(target.Schedule, ct);
                // Giao checklist công việc của ca (Task 4.3)
                await _taskAssignments.AssignForScheduleAsync(target.Schedule.Id, userId, target.Schedule.ShiftId,
                    target.Schedule.BranchId, positionId, ct);
            }, cancellationToken);
        }
        catch (Exception)
        {
            await TryDeleteImageAsync(uploaded.PublicId);
            // Hai yêu cầu check-in đồng thời cho cùng ca: UNIQUE(schedule_id) chặn yêu cầu thứ hai
            var existing = await _logs.GetByScheduleIdAsync(target.Schedule.Id, CancellationToken.None);
            if (existing != null) throw new BadRequestException($"{Sentence(target.Shift)} đã được check-in lúc {existing.CheckinTime:HH:mm}.");
            throw;
        }

        var message = log.Status == AttendanceStatus.Valid
            ? $"Check-in thành công lúc {now:HH:mm}."
            : $"Đã ghi nhận check-in lúc {now:HH:mm}, chờ Quản lý duyệt.";
        if (log.LateMinutes > target.Shift.LateGraceMinutes) message += $" Bạn đi muộn {log.LateMinutes} phút.";
        var (taskCount, _) = await _taskAssignments.CountAsync(target.Schedule.Id, mandatoryOnly: false, cancellationToken);
        if (taskCount > 0) message += $" Ca này có {taskCount} việc trong checklist.";

        if (taskCount > 0)
        {
            await _notifier.NotifyAsync(new[] { userId }, NotificationType.TaskAssigned, $"Checklist {target.Shift.Name}",
                $"Bạn có {taskCount} việc cần làm trong ca này. Mở mục Công việc để xem và tích hoàn thành.", target.Schedule.Id.ToString(), cancellationToken);
        }
        if (log.Status == AttendanceStatus.PendingApproval)
        {
            await _notifier.NotifyReviewersAsync(branch.Id, userId, NotificationType.Attendance, "Lượt chấm công cần duyệt",
                $"{employee?.FullName ?? "Nhân viên"} check-in lúc {now:HH:mm} — {string.Join(" ", warnings)}", log.Id.ToString(), cancellationToken);
        }

        return new AttendanceResultDto(log.Id, log.ScheduleId, AttendanceActions.CheckIn, now, target.Shift.Name, log.Status,
            log.LateMinutes, 0, 0, gps, WifiLayerDto.From(wifi), face, warnings, message);
    }

    // ------------------------------------------------------------------
    // Check-out
    // ------------------------------------------------------------------

    public Task<AttendanceResultDto> CheckOutAsync(AttendanceCaptureRequest request, Stream image, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default) =>
        RunAndRecordAsync(AttendanceActions.CheckOut, request, trace => CheckOutCoreAsync(request, image, fileName, contentType, length, trace, cancellationToken));

    private async Task<AttendanceResultDto> CheckOutCoreAsync(AttendanceCaptureRequest request, Stream image, string fileName, string contentType, long length,
        AttemptTrace trace, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var now = _clock.Now;
        ValidateCapture(request, contentType, length);
        trace.Stage = AttemptStage.Face;
        var profile = await GetFaceProfileAsync(userId, cancellationToken);

        trace.Stage = AttemptStage.Time;
        var open = await FindOpenAsync(userId, cancellationToken)
                   ?? throw new BadRequestException("Bạn chưa check-in ca nào nên chưa thể check-out.");
        var (log, item) = (open.Log, open.Item);
        var deadline = CheckOutDeadline(item);
        if (now > deadline)
        {
            throw new BadRequestException(
                $"Đã quá hạn check-out {Label(item.Shift)} ({deadline:HH:mm dd/MM}). Vui lòng liên hệ Quản lý để điều chỉnh giờ ra.");
        }

        trace.ScheduleId = item.Schedule.Id;
        trace.BranchId = log.BranchId;
        var branch = await GetBranchAsync(log.BranchId, cancellationToken);
        trace.Stage = AttemptStage.Gps;
        var gps = CheckGps(branch, request, userId, AttendanceActions.CheckOut, trace);

        trace.Stage = AttemptStage.Face;
        await using var photo = await BufferAsync(image, cancellationToken);
        var face = await CheckFaceAsync(photo, fileName, profile, userId, AttendanceActions.CheckOut, trace, cancellationToken);
        trace.Stage = AttemptStage.Wifi;
        var wifi = await CheckWifiAsync(branch, request, trace, cancellationToken);
        trace.Stage = AttemptStage.Save;

        var warnings = new List<string>();
        if (!wifi.IsPassed) warnings.Add(WifiWarning(wifi, checkOut: true));
        var (_, mandatoryPending) = await _taskAssignments.CountAsync(item.Schedule.Id, mandatoryOnly: true, cancellationToken);

        log.CheckoutTime = now;
        log.CheckoutLat = ToCoordinate(request.Latitude!.Value);
        log.CheckoutLng = ToCoordinate(request.Longitude!.Value);
        log.CheckoutAccuracy = request.Accuracy;
        log.CheckoutDistance = Math.Round(gps.DistanceMeters, 2);
        log.CheckoutBssid = wifi.Bssid;
        log.CheckoutWifiMatched = wifi.Status == WifiCheckStatus.Matched;
        log.CheckoutFaceMatched = true;
        log.CheckoutFaceScore = face.Score;
        log.CheckoutDeviceId = NormalizeDeviceId(request.DeviceId);
        log.EarlyMinutes = ViolationPolicy.EarlyLeaveMinutes(item.Shift, item.Schedule.WorkDate, now);
        log.ActualWorkHours = AttendanceRules.WorkedHours(item.Shift, item.Schedule.WorkDate, log.CheckinTime, now);
        var becamePending = warnings.Count > 0 && log.Status == AttendanceStatus.Valid;
        if (becamePending)
        {
            // Lượt vào đã hợp lệ (hoặc đã được duyệt) nhưng lượt ra có vấn đề → duyệt lại cả bản ghi
            log.Status = AttendanceStatus.PendingApproval;
            log.ReviewedBy = null;
            log.ReviewedAt = null;
            log.ReviewNote = null;
        }
        log.Note = JoinNote(log.Note, warnings);

        var uploaded = await UploadAsync(photo, fileName, cancellationToken);
        log.CheckoutImageUrl = uploaded.Url;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _logs.UpdateAsync(log, ct);
                item.Schedule.Status = ScheduleStatus.Completed;
                await _schedules.UpdateAsync(item.Schedule, ct);
            }, cancellationToken);
        }
        catch
        {
            await TryDeleteImageAsync(uploaded.PublicId);
            throw;
        }

        var message = warnings.Count == 0
            ? $"Check-out thành công lúc {now:HH:mm}. Giờ làm: {log.ActualWorkHours:0.##} giờ."
            : $"Đã ghi nhận check-out lúc {now:HH:mm}, chờ Quản lý duyệt.";
        if (log.EarlyMinutes > item.Shift.EarlyLeaveGraceMinutes) message += $" Bạn về sớm {log.EarlyMinutes} phút.";
        if (mandatoryPending > 0) message += $" Còn {mandatoryPending} việc bắt buộc trong checklist chưa hoàn thành.";

        if (becamePending)
        {
            var employee = await _users.GetByIdAsync(userId, cancellationToken);
            await _notifier.NotifyReviewersAsync(branch.Id, userId, NotificationType.Attendance, "Lượt chấm công cần duyệt",
                $"{employee?.FullName ?? "Nhân viên"} check-out lúc {now:HH:mm} — {string.Join(" ", warnings)}", log.Id.ToString(), cancellationToken);
        }

        return new AttendanceResultDto(log.Id, log.ScheduleId, AttendanceActions.CheckOut, now, item.Shift.Name, log.Status,
            log.LateMinutes, log.EarlyMinutes, log.ActualWorkHours, gps, WifiLayerDto.From(wifi), face, warnings, message);
    }

    // ------------------------------------------------------------------
    // Các lớp xác minh
    // ------------------------------------------------------------------

    private void ValidateCapture(AttendanceCaptureRequest request, string contentType, long length)
    {
        if (length <= 0) throw new BadRequestException("Vui lòng chụp ảnh khuôn mặt để chấm công.");
        if (length > MaxImageBytes) throw new BadRequestException("Ảnh tối đa 5 MB.");
        if (!AllowedTypes.Contains(contentType.ToLowerInvariant())) throw new BadRequestException("Ảnh phải là JPG, PNG, WEBP hoặc HEIC.");

        if (request.IsMocked)
        {
            _logger.LogWarning("Từ chối chấm công: người dùng {UserId} dùng vị trí giả lập", CurrentUserId);
            throw new BadRequestException("Phát hiện vị trí giả lập (Fake GPS). Hãy tắt ứng dụng giả lập vị trí rồi thử lại.");
        }
        if (request.Latitude is not { } lat || request.Longitude is not { } lng || !Geofence.IsValidCoordinate(lat, lng))
        {
            throw new BadRequestException("Không lấy được vị trí GPS. Hãy bật định vị và cấp quyền vị trí cho FlexiWork.");
        }
    }

    private GpsLayerDto CheckGps(Branch branch, AttendanceCaptureRequest request, int userId, string action, AttemptTrace trace)
    {
        // Không có sai số thì không biết vị trí đáng tin tới đâu → không được coi là đạt (bỏ trống trường này không né được kiểm tra)
        if (request.Accuracy is not { } accuracy || !double.IsFinite(accuracy) || accuracy <= 0)
        {
            throw new BadRequestException("Điện thoại không cung cấp sai số GPS hợp lệ. Hãy bật chế độ vị trí chính xác rồi thử lại.");
        }
        if (accuracy > _settings.MaxGpsAccuracyMeters)
        {
            throw new BadRequestException(
                $"Tín hiệu GPS yếu (sai số {accuracy:0} m). Hãy ra chỗ thoáng, bật chế độ vị trí chính xác rồi thử lại.");
        }

        var result = Geofence.Evaluate((double)branch.Latitude, (double)branch.Longitude, branch.Radius,
            request.Latitude!.Value, request.Longitude!.Value);
        trace.Distance = Math.Round(result.DistanceMeters, 2);
        if (!result.IsInside)
        {
            _logger.LogInformation("Từ chối {Action}: người dùng {UserId} cách chi nhánh {BranchId} {Distance:0} m (bán kính {Radius} m)",
                action, userId, branch.Id, result.DistanceMeters, branch.Radius);
            throw new BadRequestException(
                $"Bạn đang cách {branch.Name} khoảng {FormatDistance(result.DistanceMeters)}, ngoài phạm vi chấm công {branch.Radius} m.");
        }

        return new GpsLayerDto(true, Math.Round(result.DistanceMeters, 1), branch.Radius, request.Accuracy);
    }

    private async Task<FaceLayerDto> CheckFaceAsync(Stream photo, string fileName, FaceProfile profile, int userId, string action,
        AttemptTrace trace, CancellationToken cancellationToken)
    {
        photo.Position = 0;
        var result = await _recognition.VerifyAsync(photo, fileName, profile.FaceEncoding, cancellationToken);
        if (!result.Success) throw new BadRequestException(result.Message);
        trace.FaceScore = result.Confidence;
        trace.FaceThreshold = result.Threshold;
        if (!result.IsMatched)
        {
            _logger.LogWarning("Từ chối {Action}: khuôn mặt người dùng {UserId} không khớp (cosine {Score:0.000} < {Threshold})",
                action, userId, result.Confidence, result.Threshold);
            throw new BadRequestException("Khuôn mặt không khớp với dữ liệu đã đăng ký. Hãy nhìn thẳng vào camera, đủ sáng rồi chụp lại.");
        }
        return new FaceLayerDto(true, result.Confidence, result.Threshold);
    }

    private async Task<WifiCheckResult> CheckWifiAsync(Branch branch, AttendanceCaptureRequest request, AttemptTrace trace,
        CancellationToken cancellationToken)
    {
        var wifis = await _wifis.GetByBranchIdAsync(branch.Id, activeOnly: true, cancellationToken);
        var result = WifiVerifier.Verify(request.Bssid, wifis.Select(w => w.Bssid));
        trace.WifiStatus = result.Status switch
        {
            WifiCheckStatus.Matched => "matched",
            WifiCheckStatus.NotMatched => "not_matched",
            WifiCheckStatus.NoWifiInfo => "no_wifi_info",
            _ => "not_required"
        };
        trace.Bssid = result.Bssid;
        return result;
    }

    private static string WifiWarning(WifiCheckResult wifi, bool checkOut = false)
    {
        var when = checkOut ? "lúc ra" : "lúc vào";
        return wifi.Status == WifiCheckStatus.NoWifiInfo
            ? $"Không xác định được Wi-Fi {when} (chưa kết nối Wi-Fi chi nhánh hoặc thiết bị không cho đọc)."
            : $"Wi-Fi {when} ({wifi.Bssid}) không thuộc chi nhánh.";
    }

    // ------------------------------------------------------------------
    // Nhật ký mọi lượt chấm công (Task 6.5)
    // ------------------------------------------------------------------

    /// <summary>Những gì từng lớp xác minh đã đo được — điền dần trong lúc xử lý để ghi vào attendance_attempts.</summary>
    private sealed class AttemptTrace
    {
        public string Stage { get; set; } = AttemptStage.Validate;
        public int? BranchId { get; set; }
        public long? ScheduleId { get; set; }
        public double? Distance { get; set; }
        public double? FaceScore { get; set; }
        public double? FaceThreshold { get; set; }
        public string? WifiStatus { get; set; }
        public string? Bssid { get; set; }
    }

    /// <summary>
    /// Chạy một lượt chấm công và ghi lại kết quả (kể cả khi bị từ chối) cùng thời gian xử lý. Việc ghi nhật ký lỗi thì chỉ cảnh báo,
    /// không bao giờ làm hỏng lượt chấm công của nhân viên.
    /// </summary>
    private async Task<AttendanceResultDto> RunAndRecordAsync(string action, AttendanceCaptureRequest request,
        Func<AttemptTrace, Task<AttendanceResultDto>> run)
    {
        var userId = CurrentUserId;
        var startedAt = _clock.Now;
        var trace = new AttemptTrace();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await run(trace);
            await RecordAttemptAsync(action, userId, startedAt, request, trace, stopwatch,
                result.Status == AttendanceStatus.Valid ? AttemptOutcome.Accepted : AttemptOutcome.PendingApproval, result.Message);
            return result;
        }
        catch (AppException ex)
        {
            await RecordAttemptAsync(action, userId, startedAt, request, trace, stopwatch, AttemptOutcome.Rejected, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            await RecordAttemptAsync(action, userId, startedAt, request, trace, stopwatch, AttemptOutcome.Error, ex.GetType().Name);
            throw;
        }
    }

    private async Task RecordAttemptAsync(string action, int userId, DateTime attemptedAt, AttendanceCaptureRequest request, AttemptTrace trace,
        Stopwatch stopwatch, string outcome, string message)
    {
        try
        {
            var hasCoordinate = request.Latitude is { } lat && request.Longitude is { } lng && Geofence.IsValidCoordinate(lat, lng);
            await _attempts.AddAsync(new AttendanceAttempt
            {
                UserId = userId,
                BranchId = trace.BranchId,
                ScheduleId = trace.ScheduleId,
                Action = action,
                AttemptedAt = attemptedAt,
                Outcome = outcome,
                Stage = trace.Stage,
                Message = message.Length <= NoteMaxLength ? message : message[..(NoteMaxLength - 1)] + "…",
                Latitude = hasCoordinate ? ToCoordinate(request.Latitude!.Value) : null,
                Longitude = hasCoordinate ? ToCoordinate(request.Longitude!.Value) : null,
                Accuracy = request.Accuracy is { } acc && double.IsFinite(acc) ? acc : null,
                Distance = trace.Distance,
                IsMocked = request.IsMocked,
                Bssid = trace.Bssid,
                WifiStatus = trace.WifiStatus,
                FaceScore = trace.FaceScore,
                FaceThreshold = trace.FaceThreshold,
                DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
                DeviceId = NormalizeDeviceId(request.DeviceId)
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được nhật ký lượt chấm công của người dùng {UserId}", userId);
        }
    }

    // ------------------------------------------------------------------
    // Hỗ trợ
    // ------------------------------------------------------------------

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    private DateTime CheckOutDeadline(ScheduledShift item) => AttendanceRules.CheckOutDeadline(item, _settings.LateCheckOutMinutes);

    private sealed record OpenAttendance(AttendanceLog Log, ScheduledShift Item);

    /// <summary>Lượt đang mở (đã check-in, chưa check-out) kèm ca của nó; null nếu không có hoặc ca không còn.</summary>
    private async Task<OpenAttendance?> FindOpenAsync(int userId, CancellationToken cancellationToken)
    {
        var log = await _logs.GetOpenByUserAsync(userId, cancellationToken);
        if (log?.ScheduleId is not { } scheduleId) return null;

        var schedule = await _schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule == null) return null;
        var shift = (await _shifts.GetByIdsAsync(new[] { schedule.ShiftId }, includeDeleted: true, cancellationToken)).FirstOrDefault();
        return shift == null ? null : new OpenAttendance(log, new ScheduledShift(schedule, shift));
    }

    private async Task<IReadOnlyList<ScheduledShift>> LoadScheduledShiftsAsync(int userId, DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        var schedules = await _schedules.GetActiveByUserAsync(userId, fromDate, toDate, cancellationToken);
        if (schedules.Count == 0) return Array.Empty<ScheduledShift>();

        var shifts = (await _shifts.GetByIdsAsync(schedules.Select(s => s.ShiftId), includeDeleted: true, cancellationToken))
            .ToDictionary(s => s.Id);
        return schedules
            .Where(s => shifts.ContainsKey(s.ShiftId))
            .Select(s => new ScheduledShift(s, shifts[s.ShiftId]))
            .ToList();
    }

    private string NoCheckInMessage(IEnumerable<ScheduledShift> items, DateTime now)
    {
        var next = AttendanceRules.NextUpcoming(items, now, _settings.EarlyCheckInMinutes);
        return next == null
            ? "Bạn không có ca nào cần check-in lúc này."
            : $"Chưa tới giờ check-in {Label(next.Shift)} (mở lúc {AttendanceRules.CheckInOpensAt(next, _settings.EarlyCheckInMinutes):HH:mm}).";
    }

    private async Task<FaceProfile> GetFaceProfileAsync(int userId, CancellationToken cancellationToken)
    {
        var profile = await _faces.GetByUserIdAsync(userId, cancellationToken)
                      ?? throw new BadRequestException("Bạn chưa đăng ký khuôn mặt. Vào Hồ sơ → Khuôn mặt (Face ID) để đăng ký trước khi chấm công.");
        if (profile.ModelName != FaceProfile.DefaultModelName || profile.FaceEncoding.Length != FaceProfile.EmbeddingSize)
        {
            throw new BadRequestException("Dữ liệu khuôn mặt của bạn đã cũ. Hãy nhờ Quản lý hoặc bộ phận Nhân sự đặt lại để đăng ký lại.");
        }
        return profile;
    }

    private async Task<Branch> GetBranchAsync(int branchId, CancellationToken cancellationToken) =>
        await _branches.GetByIdAsync(branchId, cancellationToken) ?? throw new NotFoundException("Chi nhánh", branchId);

    /// <summary>Ảnh dùng 2 lần (nhận diện + lưu kho ảnh) nên đọc vào bộ nhớ một lần.</summary>
    private static async Task<MemoryStream> BufferAsync(Stream image, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken);
        return buffer;
    }

    private async Task<FileUploadResult> UploadAsync(Stream photo, string fileName, CancellationToken cancellationToken)
    {
        photo.Position = 0;
        return await _storage.UploadImageAsync(photo, fileName, StorageFolder, cancellationToken);
    }

    private async Task TryDeleteImageAsync(string publicId)
    {
        try
        {
            await _storage.DeleteAsync(publicId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được ảnh chấm công {PublicId} trên kho ảnh", publicId);
        }
    }

    private static string? NormalizeDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return null;
        var trimmed = deviceId.Trim();
        return trimmed.Length <= DeviceIdMaxLength ? trimmed : trimmed[..DeviceIdMaxLength];
    }

    private static decimal ToCoordinate(double value) => Math.Round((decimal)value, 8);

    private static string? JoinNote(string? existing, IEnumerable<string> warnings)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(existing)) parts.Add(existing);
        parts.AddRange(warnings);
        if (parts.Count == 0) return null;

        var note = string.Join(" ", parts);
        return note.Length <= NoteMaxLength ? note : note[..(NoteMaxLength - 1)] + "…";
    }

    /// <summary>
    /// Tên ca đặt giữa câu: "ca Hành chính"; tên đã bắt đầu bằng "Ca ..." (VD "Ca sáng") thì không lặp chữ "ca" → "ca sáng".
    /// </summary>
    private static string Label(Shift shift) =>
        shift.Name.StartsWith("ca ", StringComparison.OrdinalIgnoreCase) ? "c" + shift.Name[1..] : $"ca {shift.Name}";

    /// <summary>Như <see cref="Label"/> nhưng viết hoa chữ đầu (đứng đầu câu).</summary>
    private static string Sentence(Shift shift)
    {
        var label = Label(shift);
        return char.ToUpperInvariant(label[0]) + label[1..];
    }

    private static string FormatDistance(double meters) =>
        meters >= 1000 ? $"{meters / 1000:0.#} km" : $"{meters:0} m";
}
