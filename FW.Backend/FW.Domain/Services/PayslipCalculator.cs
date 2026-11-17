using FW.Domain.Enums;

namespace FW.Domain.Services;

/// <summary>Một phụ cấp nhân viên được hưởng (đã chọn mức tiền: mức riêng hoặc mức danh mục).</summary>
public sealed record PayslipAllowanceInput(string Name, string Basis, decimal UnitAmount);

/// <summary>Cấu hình lương và số liệu bảng công của một nhân viên trong tháng.</summary>
/// <param name="HourlyOverride">Đơn giá giờ riêng của nhân viên (null = theo chức vụ).</param>
/// <param name="WorkDays">Ngày công thực tế (đã trừ công bị phạt) của bảng công.</param>
/// <param name="PaidLeaveDays">Ngày nghỉ có lương theo đơn đã duyệt (đơn vị công).</param>
public sealed record PayslipInput(
    string EmploymentType,
    decimal? BaseSalary,
    decimal? HourlyOverride,
    decimal? PositionRate,
    decimal StandardWorkDays,
    decimal OvertimeMultiplier,
    decimal WorkDays,
    decimal WorkHours,
    decimal PaidLeaveDays,
    decimal OvertimeHours,
    decimal PenaltyAmount,
    int ViolationCount,
    int TotalLateMinutes,
    int TotalEarlyMinutes,
    int NightShifts,
    int SplitShiftDays,
    IReadOnlyList<PayslipAllowanceInput> Allowances);

/// <summary>Một dòng của phiếu lương; khoản trừ có <see cref="Amount"/> âm.</summary>
public sealed record PayslipLine(string Kind, string Name, decimal Quantity, decimal UnitAmount, decimal Amount, string? Note = null);

public sealed record PayslipResult(
    decimal HourlyRate,
    decimal BasicPay,
    decimal AllowanceTotal,
    decimal OvertimePay,
    decimal PenaltyTotal,
    decimal NetPay,
    IReadOnlyList<PayslipLine> Lines);

/// <summary>
/// Công thức tính phiếu lương (Task 5.5, UC 8.3) — hàm thuần, kiểm thử độc lập với CSDL. Số liệu cấu hình lấy ở Task 5.4 (<see cref="PayrollRules"/>),
/// số liệu công lấy từ bảng công đã khóa (Task 5.3). Mọi khoản tiền làm tròn tới đồng, tính từ số chưa làm tròn (không cộng dồn sai số).
/// <list type="bullet">
/// <item><b>Toàn thời gian:</b> lương theo công = lương tháng × ngày công ÷ số công chuẩn; nghỉ phép có lương tính như ngày công.</item>
/// <item><b>Bán thời gian:</b> lương theo giờ = giờ làm × đơn giá giờ; ngày nghỉ phép có lương quy đổi 8 giờ / công.</item>
/// <item><b>OT</b> = giờ OT × đơn giá giờ tính OT × hệ số OT. <b>Phụ cấp</b> theo <see cref="PayrollRules.AllowanceTotal"/> (khoản bằng 0 không hiện).</item>
/// <item><b>Phạt</b> = tổng tiền phạt của bảng công (số âm trên phiếu). <b>Thực nhận</b> = lương + phụ cấp + OT − phạt.</item>
/// </list>
/// </summary>
public static class PayslipCalculator
{
    public static PayslipResult Calculate(PayslipInput input)
    {
        var lines = new List<PayslipLine>();
        var partTime = input.EmploymentType == EmploymentType.PartTime;
        var hourlyRate = PayrollRules.EffectiveHourlyRate(input.HourlyOverride, input.PositionRate);
        var dailyRate = input.StandardWorkDays > 0 && input.BaseSalary is > 0 ? input.BaseSalary.Value / input.StandardWorkDays : 0m;

        // ---- Lương cơ bản
        decimal basicPay;
        if (partTime)
        {
            var work = Money(input.WorkHours * hourlyRate);
            var leaveHours = input.PaidLeaveDays * PayrollRules.StandardHoursPerDay;
            var leave = Money(leaveHours * hourlyRate);
            lines.Add(new PayslipLine(PayslipItemKind.Basic, "Lương theo giờ", input.WorkHours, Unit(hourlyRate), work));
            if (leave != 0) lines.Add(new PayslipLine(PayslipItemKind.Basic, "Nghỉ phép có lương", leaveHours, Unit(hourlyRate), leave, "Quy đổi 8 giờ / công"));
            basicPay = work + leave;
        }
        else
        {
            var work = Money((input.BaseSalary ?? 0) * input.WorkDays / Positive(input.StandardWorkDays));
            var leave = Money((input.BaseSalary ?? 0) * input.PaidLeaveDays / Positive(input.StandardWorkDays));
            lines.Add(new PayslipLine(PayslipItemKind.Basic, "Lương theo công", input.WorkDays, Unit(dailyRate), work));
            if (leave != 0) lines.Add(new PayslipLine(PayslipItemKind.Basic, "Nghỉ phép có lương", input.PaidLeaveDays, Unit(dailyRate), leave));
            basicPay = work + leave;
        }

        // ---- Phụ cấp
        decimal allowanceTotal = 0;
        foreach (var allowance in input.Allowances)
        {
            var amount = PayrollRules.AllowanceTotal(allowance.Basis, allowance.UnitAmount, input.NightShifts, input.SplitShiftDays);
            if (amount == 0) continue;
            var quantity = PayrollRules.AllowanceQuantity(allowance.Basis, input.NightShifts, input.SplitShiftDays);
            lines.Add(new PayslipLine(PayslipItemKind.Allowance, allowance.Name, quantity, allowance.UnitAmount, amount, AllowanceBasis.Label(allowance.Basis)));
            allowanceTotal += amount;
        }

        // ---- Làm thêm giờ
        decimal overtimePay = 0;
        if (input.OvertimeHours > 0)
        {
            var rate = PayrollRules.OvertimeHourlyRate(input.EmploymentType, input.BaseSalary, input.HourlyOverride, input.PositionRate, input.StandardWorkDays);
            overtimePay = Money(input.OvertimeHours * rate * input.OvertimeMultiplier);
            if (overtimePay != 0)
            {
                lines.Add(new PayslipLine(PayslipItemKind.Overtime, $"Làm thêm giờ (×{input.OvertimeMultiplier:0.##})", input.OvertimeHours,
                    Unit(rate * input.OvertimeMultiplier), overtimePay));
            }
        }

        // ---- Phạt
        var penalty = Money(input.PenaltyAmount);
        if (penalty > 0)
        {
            var quantity = Math.Max(1, input.ViolationCount);
            lines.Add(new PayslipLine(PayslipItemKind.Penalty, "Phạt đi muộn / về sớm", quantity, Unit(penalty / quantity), -penalty,
                $"Đi muộn {input.TotalLateMinutes} phút, về sớm {input.TotalEarlyMinutes} phút"));
        }

        var net = basicPay + allowanceTotal + overtimePay - penalty;
        return new PayslipResult(Unit(hourlyRate), basicPay, allowanceTotal, overtimePay, penalty, net, lines);
    }

    private static decimal Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static decimal Unit(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Positive(decimal value) => value > 0 ? value : 1m;
}
