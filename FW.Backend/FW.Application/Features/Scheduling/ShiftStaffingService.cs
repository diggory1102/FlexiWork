using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.Application.Features.Scheduling;

public interface IShiftStaffingService
{
    /// <summary>Mọi ngoại lệ theo thứ trong tuần và các ngoại lệ theo ngày từ hôm nay trở đi của chi nhánh.</summary>
    Task<IReadOnlyList<StaffingRuleDto>> GetRulesAsync(int? branchId, CancellationToken cancellationToken = default);

    /// <summary>Đặt ngoại lệ cho một (chi nhánh, ca, ngày / thứ); đã có thì thay bằng giá trị mới.</summary>
    Task<StaffingRuleDto> SaveAsync(StaffingRuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Bỏ ngoại lệ — ô đó quay về định biên mặc định của ca.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Định biên hiệu lực (sau khi áp ngoại lệ) của các ca của chi nhánh trong khoảng ngày; chỉ trả những ô có giới hạn.</summary>
    Task<IReadOnlyList<EffectiveStaffingDto>> GetEffectiveAsync(int? branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ngoại lệ định biên theo chi nhánh / ca / ngày hoặc thứ trong tuần (Task 6.6, bước 2): VD ngày có tiệc đặt bàn cần thêm người, cuối tuần đông hơn.
/// Chỉ đổi số người được phép xếp thêm — không đụng tới giờ giấc của ca và không xóa ai đã được xếp.
/// Quản lý đặt cho chi nhánh mình; Admin/HR cho mọi chi nhánh. Chỉ đặt được cho hôm nay trở đi.
/// </summary>
public class ShiftStaffingService : IShiftStaffingService
{
    public const int MaxRangeDays = ScheduleService.MaxRangeDays;

    private readonly IShiftStaffingRuleRepository _rules;
    private readonly IShiftRepository _shifts;
    private readonly IBranchRepository _branches;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ShiftStaffingService(
        IShiftStaffingRuleRepository rules,
        IShiftRepository shifts,
        IBranchRepository branches,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _rules = rules;
        _shifts = shifts;
        _branches = branches;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<StaffingRuleDto>> GetRulesAsync(int? branchId, CancellationToken cancellationToken = default)
    {
        var branch = ResolveBranchId(branchId);
        var rules = await _rules.GetUpcomingAsync(branch, _clock.Today.ToDateTime(TimeOnly.MinValue), cancellationToken);
        return await ToDtosAsync(rules, cancellationToken);
    }

    public async Task<StaffingRuleDto> SaveAsync(StaffingRuleRequest request, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchId(request.BranchId);
        var branch = await _branches.GetByIdAsync(branchId, cancellationToken) ?? throw new NotFoundException("Chi nhánh", branchId);
        if (branch.Status != BranchStatus.Active) throw new BadRequestException("Chi nhánh đã ngừng hoạt động.");

        var shift = (await _shifts.GetAvailableForBranchAsync(branchId, cancellationToken)).FirstOrDefault(s => s.Id == request.ShiftId)
                    ?? throw new BadRequestException("Ca không tồn tại hoặc không áp dụng cho chi nhánh này.");
        if (request is { MinStaff: { } min, MaxStaff: { } max } && min > max)
        {
            throw new BadRequestException("Số người tối thiểu không được lớn hơn số người tối đa.");
        }

        var rule = new ShiftStaffingRule
        {
            BranchId = branchId,
            ShiftId = shift.Id,
            ScopeKey = BuildScopeKey(request),
            MinStaff = request.MinStaff,
            MaxStaff = request.MaxStaff,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedBy = _currentUser.UserId
        };
        await _rules.UpsertAsync(rule, cancellationToken);

        var saved = await _rules.GetByIdAsync(rule.Id, cancellationToken) ?? rule;
        return StaffingRuleDto.From(saved, shift);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Ngoại lệ định biên", id);
        _currentUser.EnsureCanAccessBranch(rule.BranchId);
        await _rules.DeleteAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<EffectiveStaffingDto>> GetEffectiveAsync(int? branchId, DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var branch = ResolveBranchId(branchId);
        if (fromDate == default || toDate == default) throw new BadRequestException("Vui lòng chọn khoảng ngày.");
        if (toDate < fromDate) throw new BadRequestException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (toDate.DayNumber - fromDate.DayNumber + 1 > MaxRangeDays)
        {
            throw new BadRequestException($"Mỗi lần chỉ xem tối đa {MaxRangeDays} ngày.");
        }

        var shifts = await _shifts.GetAvailableForBranchAsync(branch, cancellationToken);
        var rules = (await _rules.GetForRangeAsync(branch, fromDate.ToDateTime(TimeOnly.MinValue), toDate.ToDateTime(TimeOnly.MinValue), cancellationToken))
            .ToLookup(r => r.ShiftId);

        var result = new List<EffectiveStaffingDto>();
        foreach (var shift in shifts)
        {
            var shiftRules = rules[shift.Id].ToList();
            for (var day = fromDate; day <= toDate; day = day.AddDays(1))
            {
                var limits = ShiftStaffing.Resolve(shift, shiftRules, day.ToDateTime(TimeOnly.MinValue));
                if (limits.IsLimited)
                {
                    result.Add(new EffectiveStaffingDto(shift.Id, day, limits.Min, limits.Max, EffectiveStaffingDto.SourceName(limits.Source), limits.Note));
                }
            }
        }
        return result;
    }

    // ------------------------------------------------------------------

    /// <summary>Quản lý: bỏ trống = chi nhánh của mình, chọn chi nhánh khác bị từ chối. Admin/HR: bắt buộc chọn chi nhánh.</summary>
    private int ResolveBranchId(int? requested)
    {
        var branchId = requested ?? _currentUser.GetBranchScope() ?? throw new BadRequestException("Vui lòng chọn chi nhánh.");
        _currentUser.EnsureCanAccessBranch(branchId);
        return branchId;
    }

    private string BuildScopeKey(StaffingRuleRequest request)
    {
        switch (request.Scope)
        {
            case StaffingScopes.Date:
            {
                var date = request.Date ?? throw new BadRequestException("Vui lòng chọn ngày áp dụng.");
                if (date < _clock.Today) throw new BadRequestException("Không đặt được ngoại lệ cho ngày đã qua.");
                if (date.DayNumber - _clock.Today.DayNumber > ScheduleService.MaxAdvanceDays)
                {
                    throw new BadRequestException($"Chỉ đặt ngoại lệ trước tối đa {ScheduleService.MaxAdvanceDays} ngày.");
                }
                return ShiftStaffing.DateKey(date.ToDateTime(TimeOnly.MinValue));
            }
            case StaffingScopes.Weekday:
                return ShiftStaffing.WeekdayKey(request.DayOfWeek ?? throw new BadRequestException("Vui lòng chọn thứ trong tuần."));
            default:
                throw new BadRequestException("Phạm vi áp dụng không hợp lệ (date hoặc weekday).");
        }
    }

    private async Task<IReadOnlyList<StaffingRuleDto>> ToDtosAsync(IReadOnlyList<ShiftStaffingRule> rules, CancellationToken cancellationToken)
    {
        if (rules.Count == 0) return Array.Empty<StaffingRuleDto>();

        var shifts = (await _shifts.GetByIdsAsync(rules.Select(r => r.ShiftId), includeDeleted: true, cancellationToken)).ToDictionary(s => s.Id);
        return rules.Where(r => shifts.ContainsKey(r.ShiftId)).Select(r => StaffingRuleDto.From(r, shifts[r.ShiftId])).ToList();
    }
}
