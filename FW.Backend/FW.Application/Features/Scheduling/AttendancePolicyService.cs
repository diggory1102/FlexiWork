using System.Globalization;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Scheduling;

/// <summary>Khóa trong bảng system_settings.</summary>
public static class SystemSettingKeys
{
    /// <summary>Số công chuẩn trong tháng (lương tháng ÷ công chuẩn × ngày công thực tế).</summary>
    public const string StandardWorkDays = "standard_work_days";
}

public interface IAttendancePolicyService
{
    Task<AttendancePolicyDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Lưu số công chuẩn và thay toàn bộ bậc xử lý vi phạm (một transaction).</summary>
    Task<AttendancePolicyDto> UpdateAsync(AttendancePolicyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Các bậc xử lý vi phạm hiện hành — dùng khi chấm công / tổng hợp bảng công.</summary>
    Task<IReadOnlyList<ViolationRule>> GetRulesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cấu hình số công chuẩn & quy tắc phạt (UC 1.5). Quyền ghi: Admin/HR (giới hạn ở Controller).
/// Quy tắc theo bậc: mỗi loại vi phạm có nhiều bậc "từ X phút", bậc sau không được nhẹ hơn bậc trước.
/// </summary>
public class AttendancePolicyService : IAttendancePolicyService
{
    public const decimal DefaultStandardWorkDays = 26;
    public const int MaxTiersPerType = 10;

    private readonly IViolationRuleRepository _rules;
    private readonly ISystemSettingRepository _settings;
    private readonly IUnitOfWork _unitOfWork;

    public AttendancePolicyService(IViolationRuleRepository rules, ISystemSettingRepository settings, IUnitOfWork unitOfWork)
    {
        _rules = rules;
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<AttendancePolicyDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _settings.GetValueAsync(SystemSettingKeys.StandardWorkDays, cancellationToken);
        var standardWorkDays = decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : DefaultStandardWorkDays;

        var rules = await _rules.GetAllAsync(cancellationToken);
        return new AttendancePolicyDto(standardWorkDays, rules.Select(ViolationRuleDto.From).ToList());
    }

    public Task<IReadOnlyList<ViolationRule>> GetRulesAsync(CancellationToken cancellationToken = default) =>
        _rules.GetAllAsync(cancellationToken);

    public async Task<AttendancePolicyDto> UpdateAsync(AttendancePolicyRequest request, CancellationToken cancellationToken = default)
    {
        var rules = Validate(request.Rules);
        var standardWorkDays = Math.Round(request.StandardWorkDays, 1);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _settings.SetValueAsync(SystemSettingKeys.StandardWorkDays,
                standardWorkDays.ToString(CultureInfo.InvariantCulture), cancellationToken: ct);
            await _rules.ReplaceAllAsync(rules, ct);
        }, cancellationToken);

        return await GetAsync(cancellationToken);
    }

    /// <summary>Chuẩn hóa và kiểm tra bộ quy tắc; trả về danh sách bậc sắp theo loại → số phút.</summary>
    private static List<ViolationRule> Validate(IReadOnlyList<ViolationRuleRequest> requests)
    {
        var rules = requests.Select(r => new ViolationRule
        {
            ViolationType = (r.ViolationType ?? string.Empty).Trim().ToLowerInvariant(),
            MinMinutes = r.MinMinutes,
            DeductWorkUnits = Math.Round(r.DeductWorkUnits, 2),
            PenaltyAmount = Math.Round(r.PenaltyAmount, 0),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim()
        }).ToList();

        var errors = new List<string>();
        foreach (var rule in rules)
        {
            if (!ViolationType.IsValid(rule.ViolationType))
                errors.Add($"Loại vi phạm '{rule.ViolationType}' không hợp lệ. Giá trị cho phép: {string.Join(", ", ViolationType.All)}.");
            else if (rule.DeductWorkUnits == 0 && rule.PenaltyAmount == 0)
                errors.Add($"{Label(rule.ViolationType)} từ {rule.MinMinutes} phút: phải trừ công hoặc phạt tiền (hoặc xóa bậc này).");
        }

        foreach (var group in rules.Where(r => ViolationType.IsValid(r.ViolationType)).GroupBy(r => r.ViolationType))
        {
            var label = Label(group.Key);
            var tiers = group.OrderBy(r => r.MinMinutes).ToList();

            if (tiers.Count > MaxTiersPerType)
                errors.Add($"{label}: tối đa {MaxTiersPerType} bậc.");

            foreach (var duplicate in tiers.GroupBy(r => r.MinMinutes).Where(g => g.Count() > 1))
                errors.Add($"{label}: có nhiều bậc cùng mốc {duplicate.Key} phút.");

            for (var i = 1; i < tiers.Count; i++)
            {
                if (tiers[i].DeductWorkUnits < tiers[i - 1].DeductWorkUnits || tiers[i].PenaltyAmount < tiers[i - 1].PenaltyAmount)
                    errors.Add($"{label}: bậc từ {tiers[i].MinMinutes} phút không được nhẹ hơn bậc từ {tiers[i - 1].MinMinutes} phút.");
            }
        }

        if (errors.Count > 0) throw new BadRequestException("Quy tắc vi phạm chưa hợp lệ.", errors.Distinct().ToList());

        return rules.OrderBy(r => r.ViolationType).ThenBy(r => r.MinMinutes).ToList();
    }

    private static string Label(string type) => type == ViolationType.Late ? "Đi muộn" : "Về sớm";
}
