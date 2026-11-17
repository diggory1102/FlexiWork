using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>
/// Quy tắc cấu hình & đơn giá lương (Task 5.4, UC 8.1) — hàm thuần, Task 5.5 dùng lại khi tính bảng lương.
/// <list type="bullet">
/// <item><b>Toàn thời gian:</b> lương tháng ÷ số công chuẩn × ngày công thực tế. <b>Bán thời gian:</b> giờ công × đơn giá giờ.</item>
/// <item><b>Đơn giá giờ</b> = mức riêng của nhân viên nếu có, ngược lại theo chức vụ.</item>
/// <item><b>Đơn giá giờ để tính OT:</b> bán thời gian dùng đơn giá giờ; toàn thời gian = lương tháng ÷ công chuẩn ÷ 8 giờ (chưa có lương tháng thì
/// dùng đơn giá giờ). Tiền OT = giờ OT × đơn giá giờ × hệ số OT.</item>
/// <item><b>Phụ cấp:</b> số lượng (1 / số ca đêm / số ngày ca gãy) × mức tiền (mức riêng của nhân viên nếu có).</item>
/// </list>
/// </summary>
public static class PayrollRules
{
    /// <summary>Số giờ của một ngày công (quy đổi đơn giá giờ cho nhân viên toàn thời gian).</summary>
    public const decimal StandardHoursPerDay = 8m;

    public const decimal DefaultOvertimeMultiplier = 1.5m;
    public const decimal MinOvertimeMultiplier = 1m;
    public const decimal MaxOvertimeMultiplier = 5m;

    public const decimal MaxMonthlySalary = 1_000_000_000m;
    public const decimal MaxHourlyRate = 10_000_000m;
    public const decimal MaxAllowanceAmount = 100_000_000m;

    /// <summary>Đơn giá giờ: mức riêng của nhân viên, nếu chưa có thì theo chức vụ (0 nếu cũng chưa có chức vụ).</summary>
    public static decimal EffectiveHourlyRate(decimal? hourlyOverride, decimal? positionRate) => hourlyOverride ?? positionRate ?? 0m;

    /// <summary>Đơn giá giờ dùng tính tiền OT (chưa nhân hệ số OT).</summary>
    public static decimal OvertimeHourlyRate(string employmentType, decimal? baseSalary, decimal? hourlyOverride, decimal? positionRate,
        decimal standardWorkDays)
    {
        if (employmentType == EmploymentType.FullTime && baseSalary is > 0 && standardWorkDays > 0)
        {
            return Math.Round(baseSalary.Value / standardWorkDays / StandardHoursPerDay, 2, MidpointRounding.AwayFromZero);
        }
        return EffectiveHourlyRate(hourlyOverride, positionRate);
    }

    /// <summary>Đã đủ thông tin để tính lương: toàn thời gian cần lương tháng; bán thời gian cần đơn giá giờ.</summary>
    public static bool IsConfigured(string employmentType, decimal? baseSalary, decimal effectiveHourlyRate) =>
        employmentType == EmploymentType.PartTime ? effectiveHourlyRate > 0 : baseSalary is > 0;

    /// <summary>Số lượng tính phụ cấp theo bảng công.</summary>
    public static decimal AllowanceQuantity(string basis, int nightShifts, int splitShiftDays) => basis switch
    {
        AllowanceBasis.NightShift => nightShifts,
        AllowanceBasis.SplitDay => splitShiftDays,
        _ => 1m
    };

    /// <summary>Mức tiền áp dụng: mức riêng của nhân viên nếu có, ngược lại mức của danh mục.</summary>
    public static decimal AllowanceUnitAmount(decimal catalogAmount, decimal? userOverride) => userOverride ?? catalogAmount;

    /// <summary>Thành tiền một phụ cấp trong tháng (làm tròn tới đồng).</summary>
    public static decimal AllowanceTotal(string basis, decimal unitAmount, int nightShifts, int splitShiftDays) =>
        Math.Round(AllowanceQuantity(basis, nightShifts, splitShiftDays) * unitAmount, 0, MidpointRounding.AwayFromZero);

    public static bool IsValidOvertimeMultiplier(decimal value) => value is >= MinOvertimeMultiplier and <= MaxOvertimeMultiplier;
}
