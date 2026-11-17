using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.ShiftTasks;

/// <summary>Ảnh hiện trường gửi kèm khi tích hoàn thành.</summary>
public record TaskPhoto(Stream Content, string FileName, string ContentType, long Length);

public interface IShiftTaskService
{
    /// <summary>Mẫu checklist: Admin/HR mọi chi nhánh, Quản lý việc áp dụng cho chi nhánh mình.</summary>
    Task<IReadOnlyList<ShiftTaskTemplateDto>> GetTemplatesAsync(ShiftTaskTemplateFilter filter, CancellationToken cancellationToken = default);

    Task<ShiftTaskTemplateDto> CreateTemplateAsync(ShiftTaskTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ShiftTaskTemplateDto> UpdateTemplateAsync(int id, ShiftTaskTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Checklist các ca của tôi trong ngày (mặc định hôm nay, gồm ca đêm hôm qua còn đang làm) — UC 6.2.</summary>
    Task<IReadOnlyList<ScheduleTasksDto>> GetMyTasksAsync(DateOnly? date, CancellationToken cancellationToken = default);

    /// <summary>Checklist của một ca: người làm ca hoặc người quản lý chi nhánh.</summary>
    Task<ScheduleTasksDto> GetScheduleTasksAsync(long scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Tích hoàn thành kèm ảnh hiện trường (UC 6.3).</summary>
    Task<ScheduleTasksDto> CompleteAsync(long assignmentId, CompleteTaskForm form, TaskPhoto? photo, CancellationToken cancellationToken = default);

    /// <summary>Bỏ tích (làm lại) khi ca còn đang diễn ra.</summary>
    Task<ScheduleTasksDto> UncompleteAsync(long assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Tiến độ checklist các ca trong ngày (UC 6.4).</summary>
    Task<TaskProgressDto> GetProgressAsync(TaskProgressFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Checklist công việc theo ca (Task 4.3). Quyền mẫu checklist theo ma trận phân quyền: Admin thêm / sửa / xóa mọi việc,
/// Quản lý thêm / sửa / xóa việc riêng của chi nhánh mình (kể cả trên ca dùng chung), HR chỉ xem. Việc được giao tự động khi
/// nhân viên check-in (<see cref="IShiftTaskAssignmentRepository.AssignForScheduleAsync"/>, gọi trong AttendanceService);
/// việc mới thêm vào mẫu khi ca đang diễn ra được bổ sung khi nhân viên mở checklist.
/// </summary>
public class ShiftTaskService : IShiftTaskService
{
    public const string StorageFolder = "tasks";
    public const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly IReadOnlyList<string> AllowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };

    private readonly IShiftTaskRepository _tasks;
    private readonly IShiftTaskAssignmentRepository _assignments;
    private readonly IShiftRepository _shifts;
    private readonly IScheduleRepository _schedules;
    private readonly IUserRepository _users;
    private readonly IPositionRepository _positions;
    private readonly IBranchRepository _branches;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ShiftTaskService> _logger;

    public ShiftTaskService(
        IShiftTaskRepository tasks,
        IShiftTaskAssignmentRepository assignments,
        IShiftRepository shifts,
        IScheduleRepository schedules,
        IUserRepository users,
        IPositionRepository positions,
        IBranchRepository branches,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<ShiftTaskService> logger)
    {
        _tasks = tasks;
        _assignments = assignments;
        _shifts = shifts;
        _schedules = schedules;
        _users = users;
        _positions = positions;
        _branches = branches;
        _storage = storage;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Mẫu checklist

    public async Task<IReadOnlyList<ShiftTaskTemplateDto>> GetTemplatesAsync(ShiftTaskTemplateFilter filter, CancellationToken cancellationToken = default)
    {
        var scope = _currentUser.GetBranchScope(); // null = toàn doanh nghiệp
        var branchId = scope ?? filter.BranchId;
        var items = await _tasks.GetTemplatesAsync(new ShiftTaskQuery { ShiftId = filter.ShiftId, BranchId = branchId }, cancellationToken);
        return items.Select(t => ShiftTaskTemplateDto.From(t, CanEdit(t))).ToList();
    }

    public async Task<ShiftTaskTemplateDto> CreateTemplateAsync(ShiftTaskTemplateRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanWrite();
        var shift = await _shifts.GetByIdAsync(request.ShiftId ?? 0, cancellationToken) ?? throw new BadRequestException("Ca không tồn tại.");

        // Chi nhánh của việc: Quản lý → chi nhánh mình; ca riêng của chi nhánh → chi nhánh đó; Admin để trống = mọi chi nhánh
        int? branchId = _currentUser.IsBranchManager() ? _currentUser.BranchId : request.BranchId;
        if (_currentUser.IsBranchManager() && request.BranchId.HasValue && request.BranchId != _currentUser.BranchId)
        {
            throw new ForbiddenException("Bạn chỉ thêm được việc cho chi nhánh mình quản lý.");
        }
        if (shift.BranchId.HasValue)
        {
            if (branchId.HasValue && branchId != shift.BranchId)
            {
                throw new BadRequestException("Ca này là ca riêng của chi nhánh khác.");
            }
            branchId = shift.BranchId;
        }
        if (branchId.HasValue && await _branches.GetByIdAsync(branchId.Value, cancellationToken) is null)
        {
            throw new BadRequestException("Chi nhánh không tồn tại.");
        }
        if (await _tasks.CountInScopeAsync(shift.Id, branchId, cancellationToken) >= ShiftTaskRules.MaxTasksPerShift)
        {
            throw new BadRequestException($"Mỗi ca tối đa {ShiftTaskRules.MaxTasksPerShift} việc trong checklist.");
        }

        var task = new ShiftTask { ShiftId = shift.Id, BranchId = branchId };
        await ApplyAsync(task, request, cancellationToken);
        await _tasks.AddAsync(task, cancellationToken);
        return await GetTemplateDtoAsync(task.Id, cancellationToken);
    }

    public async Task<ShiftTaskTemplateDto> UpdateTemplateAsync(int id, ShiftTaskTemplateRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanWrite();
        var task = await GetEditableAsync(id, cancellationToken);
        await ApplyAsync(task, request, cancellationToken);
        await _tasks.UpdateAsync(task, cancellationToken);
        return await GetTemplateDtoAsync(id, cancellationToken);
    }

    /// <summary>Xóa mềm: việc đã giao trong các ca trước vẫn giữ để đối soát; ca sau không giao việc này nữa.</summary>
    public async Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureCanWrite();
        await GetEditableAsync(id, cancellationToken);
        await _tasks.DeleteAsync(id, cancellationToken);
    }

    private void EnsureCanWrite()
    {
        if (!_currentUser.IsInRole(RoleNames.Admin) && !_currentUser.IsBranchManager())
        {
            throw new ForbiddenException("Chỉ Quản trị viên và Quản lý chi nhánh được thay đổi checklist công việc.");
        }
    }

    private bool CanEdit(ShiftTask task) =>
        _currentUser.IsInRole(RoleNames.Admin) ||
        (_currentUser.IsBranchManager() && task.BranchId.HasValue && task.BranchId == _currentUser.BranchId);

    private async Task<ShiftTask> GetEditableAsync(int id, CancellationToken cancellationToken)
    {
        var task = await _tasks.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Việc trong checklist", id);
        if (!CanEdit(task))
        {
            throw new ForbiddenException(task.BranchId is null
                ? "Việc này áp dụng cho mọi chi nhánh — chỉ Quản trị viên sửa / xóa được."
                : "Việc này thuộc chi nhánh khác.");
        }
        return task;
    }

    private async Task ApplyAsync(ShiftTask task, ShiftTaskTemplateRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (title.Length == 0) throw new BadRequestException("Vui lòng nhập tên việc.");
        if (!TaskPhase.IsValid(request.Phase)) throw new BadRequestException("Giai đoạn không hợp lệ (start, during, end).");
        if (request.PositionId is { } positionId && !await _positions.ExistsAsync(positionId, cancellationToken))
        {
            throw new BadRequestException("Chức vụ không tồn tại.");
        }

        task.Title = title;
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.PositionId = request.PositionId;
        task.Phase = request.Phase;
        task.SortOrder = request.SortOrder;
        task.IsMandatory = request.IsMandatory;
        task.RequiresPhoto = request.RequiresPhoto;
    }

    private async Task<ShiftTaskTemplateDto> GetTemplateDtoAsync(int id, CancellationToken cancellationToken)
    {
        var task = await _tasks.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Việc trong checklist", id);
        var detail = (await _tasks.GetTemplatesAsync(new ShiftTaskQuery { ShiftId = task.ShiftId }, cancellationToken)).Single(t => t.Id == id);
        return ShiftTaskTemplateDto.From(detail, CanEdit(detail));
    }

    // ------------------------------------------------------------------ Checklist trong ca

    public async Task<IReadOnlyList<ScheduleTasksDto>> GetMyTasksAsync(DateOnly? date, CancellationToken cancellationToken = default)
    {
        var me = await _users.GetByIdAsync(CurrentUserId, cancellationToken) ?? throw new UnauthorizedException();
        var day = (date ?? _clock.Today).ToDateTime(TimeOnly.MinValue);

        // Ca trong ngày + ca đêm hôm trước còn đang làm
        var schedules = (await _schedules.GetActiveByUserAsync(me.Id, day.AddDays(-1), day, cancellationToken))
            .Where(s => s.IsPublished && s.Status != ScheduleStatus.OnLeave &&
                        (s.WorkDate.Date == day || s.Status == ScheduleStatus.InProgress))
            .ToList();

        var result = new List<ScheduleTasksDto>();
        foreach (var schedule in schedules)
        {
            result.Add(await BuildAsync(schedule, me, cancellationToken));
        }
        return result;
    }

    public async Task<ScheduleTasksDto> GetScheduleTasksAsync(long scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _schedules.GetByIdAsync(scheduleId, cancellationToken) ?? throw new NotFoundException("Lịch làm việc", scheduleId);
        EnsureCanView(schedule);
        var owner = await _users.GetByIdAsync(schedule.UserId, cancellationToken) ?? throw new NotFoundException("Nhân viên", schedule.UserId);
        return await BuildAsync(schedule, owner, cancellationToken);
    }

    public async Task<ScheduleTasksDto> CompleteAsync(long assignmentId, CompleteTaskForm form, TaskPhoto? photo,
        CancellationToken cancellationToken = default)
    {
        var assignment = await GetOwnAssignmentAsync(assignmentId, cancellationToken);
        if (assignment.IsCompleted) throw new BadRequestException("Việc này đã hoàn thành — bỏ tích trước nếu muốn làm lại.");
        Ensure(ShiftTaskRules.ValidateCompletion(assignment.Task, photo is not null));
        if (photo is not null) ValidateImage(photo);

        var note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();
        if (note?.Length > ShiftTaskRules.MaxNoteLength) throw new BadRequestException("Ghi chú tối đa 255 ký tự.");

        FileUploadResult? uploaded = null;
        if (photo is not null) uploaded = await _storage.UploadImageAsync(photo.Content, photo.FileName, StorageFolder, cancellationToken);

        assignment.IsCompleted = true;
        assignment.CompletedAt = _clock.Now;
        assignment.ProofImageUrl = uploaded?.Url;
        assignment.ProofImagePublicId = uploaded?.PublicId;
        assignment.Note = note;
        try
        {
            await _assignments.UpdateAsync(ToEntity(assignment), cancellationToken);
        }
        catch
        {
            if (uploaded is not null) await TryDeleteImageAsync(uploaded.PublicId);
            throw;
        }
        return await GetScheduleTasksAsync(assignment.ScheduleId, cancellationToken);
    }

    public async Task<ScheduleTasksDto> UncompleteAsync(long assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await GetOwnAssignmentAsync(assignmentId, cancellationToken);
        if (!assignment.IsCompleted) throw new BadRequestException("Việc này chưa hoàn thành.");

        var oldImage = assignment.ProofImagePublicId;
        assignment.IsCompleted = false;
        assignment.CompletedAt = null;
        assignment.ProofImageUrl = null;
        assignment.ProofImagePublicId = null;
        assignment.Note = null;
        await _assignments.UpdateAsync(ToEntity(assignment), cancellationToken);
        if (oldImage is not null) await TryDeleteImageAsync(oldImage);
        return await GetScheduleTasksAsync(assignment.ScheduleId, cancellationToken);
    }

    // ------------------------------------------------------------------ Tiến độ

    public async Task<TaskProgressDto> GetProgressAsync(TaskProgressFilter filter, CancellationToken cancellationToken = default)
    {
        var scope = _currentUser.GetBranchScope();
        var branchId = filter.BranchId ?? scope;
        if (branchId.HasValue) _currentUser.EnsureCanAccessBranch(branchId);
        var day = (filter.Date ?? _clock.Today).ToDateTime(TimeOnly.MinValue);

        var rows = await _assignments.GetProgressAsync(day, branchId, cancellationToken);
        var total = rows.Sum(r => r.Total);
        var done = rows.Sum(r => r.Done);
        return new TaskProgressDto(day, branchId, total, done, ShiftTaskRules.Percent(done, total),
            rows.Sum(r => r.MandatoryTotal - r.MandatoryDone),
            rows.Count(r => r.ScheduleStatus == ScheduleStatus.Scheduled),
            rows.Select(ScheduleProgressDto.From).ToList());
    }

    // ------------------------------------------------------------------ Tiện ích

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    private void EnsureCanView(Schedule schedule)
    {
        var canView = schedule.UserId == CurrentUserId || _currentUser.HasCompanyWideAccess() ||
                      (_currentUser.IsBranchManager() && _currentUser.CanAccessBranch(schedule.BranchId));
        if (!canView) throw new ForbiddenException("Bạn không có quyền xem checklist của ca này.");
    }

    /// <summary>Ca đã giao việc → danh sách việc (ca đang diễn ra thì bổ sung việc mới thêm vào mẫu); chưa check-in → việc dự kiến.</summary>
    private async Task<ScheduleTasksDto> BuildAsync(Schedule schedule, User owner, CancellationToken cancellationToken)
    {
        var shift = (await _shifts.GetByIdsAsync(new[] { schedule.ShiftId }, includeDeleted: true, cancellationToken)).Single();
        if (schedule.Status == ScheduleStatus.InProgress)
        {
            await _assignments.AssignForScheduleAsync(schedule.Id, schedule.UserId, schedule.ShiftId, schedule.BranchId, owner.PositionId,
                cancellationToken);
        }

        var assigned = await _assignments.GetDetailsByScheduleIdsAsync(new[] { schedule.Id }, cancellationToken);
        IReadOnlyList<TaskItemDto> tasks;
        var isAssigned = assigned.Count > 0 || schedule.Status is ScheduleStatus.InProgress or ScheduleStatus.Completed;
        if (isAssigned)
        {
            tasks = ShiftTaskRules.Sort(assigned, a => a.Task).Select(TaskItemDto.From).ToList();
        }
        else
        {
            var preview = schedule.Status == ScheduleStatus.Scheduled
                ? await _tasks.GetApplicableAsync(schedule.ShiftId, schedule.BranchId, owner.PositionId, cancellationToken)
                : Array.Empty<ShiftTask>();
            tasks = ShiftTaskRules.Sort(preview, t => t).Select(TaskItemDto.Preview).ToList();
        }

        var done = tasks.Count(t => t.IsCompleted);
        var total = isAssigned ? tasks.Count : 0;
        return new ScheduleTasksDto(
            schedule.Id, owner.Id, owner.EmployeeCode, owner.FullName, schedule.WorkDate, shift.Name,
            shift.StartTime.ToString(@"hh\:mm"), shift.EndTime.ToString(@"hh\:mm"), schedule.Status, isAssigned,
            CanUpdate: schedule.UserId == _currentUser.UserId && ShiftTaskRules.ValidateCanUpdate(schedule) is null,
            total, done, ShiftTaskRules.Percent(done, total), tasks);
    }

    private async Task<ShiftTaskAssignmentDetail> GetOwnAssignmentAsync(long id, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Việc được giao", id);
        if (assignment.AssignedTo != CurrentUserId || assignment.ScheduleUserId != CurrentUserId)
        {
            throw new ForbiddenException("Việc này không được giao cho bạn.");
        }
        Ensure(ShiftTaskRules.ValidateCanUpdate(new Schedule { Status = assignment.ScheduleStatus }));
        return assignment;
    }

    private static ShiftTaskAssignment ToEntity(ShiftTaskAssignmentDetail a) => new()
    {
        Id = a.Id, TaskId = a.TaskId, ScheduleId = a.ScheduleId, AssignedTo = a.AssignedTo, IsCompleted = a.IsCompleted,
        CompletedAt = a.CompletedAt, ProofImageUrl = a.ProofImageUrl, ProofImagePublicId = a.ProofImagePublicId, Note = a.Note
    };

    private static void ValidateImage(TaskPhoto file)
    {
        if (file.Length <= 0) throw new BadRequestException("Ảnh hiện trường bị rỗng.");
        if (file.Length > MaxImageBytes) throw new BadRequestException("Ảnh hiện trường tối đa 5 MB.");
        if (!AllowedTypes.Contains(file.ContentType.ToLowerInvariant())) throw new BadRequestException("Ảnh phải là JPG, PNG, WEBP hoặc HEIC.");
    }

    private async Task TryDeleteImageAsync(string publicId)
    {
        try
        {
            await _storage.DeleteAsync(publicId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được ảnh hiện trường {PublicId}", publicId);
        }
    }

    private static void Ensure(string? error)
    {
        if (error is not null) throw new BadRequestException(error);
    }
}
