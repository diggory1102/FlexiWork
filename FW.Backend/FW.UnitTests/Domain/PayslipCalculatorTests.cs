using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class PayslipCalculatorTests
{
    private static readonly PayslipAllowanceInput Night = new("Phụ cấp ca đêm", AllowanceBasis.NightShift, 30000m);
    private static readonly PayslipAllowanceInput Split = new("Phụ cấp ca gãy", AllowanceBasis.SplitDay, 20000m);
    private static readonly PayslipAllowanceInput Lunch = new("Phụ cấp ăn trưa", AllowanceBasis.Monthly, 500000m);

    private static PayslipInput FullTime(decimal workDays = 26, decimal paidLeave = 0, decimal ot = 0, decimal penalty = 0, int nights = 0, int splits = 0,
        decimal salary = 10_400_000m, params PayslipAllowanceInput[] allowances) => new(
        EmploymentType.FullTime, salary, null, 35000m, 26m, 1.5m, workDays, workDays * 8, paidLeave, ot, penalty, penalty > 0 ? 2 : 0, penalty > 0 ? 30 : 0,
        penalty > 0 ? 10 : 0, nights, splits, allowances);

    private static PayslipInput PartTime(decimal hours = 80, decimal paidLeave = 0, decimal ot = 0, decimal? hourly = null, decimal position = 35000m) => new(
        EmploymentType.PartTime, null, hourly, position, 26m, 1.5m, 0, hours, paidLeave, ot, 0, 0, 0, 0, 0, 0, Array.Empty<PayslipAllowanceInput>());

    [Fact]
    public void Full_time_pay_is_salary_times_work_days_over_standard_days()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 20));

        // 10.400.000 × 20 ÷ 26 = 8.000.000
        Assert.Equal(8_000_000m, result.BasicPay);
        Assert.Equal(8_000_000m, result.NetPay);
        var line = Assert.Single(result.Lines);
        Assert.Equal((PayslipItemKind.Basic, "Lương theo công", 20m, 400000m, 8_000_000m), (line.Kind, line.Name, line.Quantity, line.UnitAmount, line.Amount));
    }

    [Fact]
    public void A_full_month_pays_the_whole_salary() => Assert.Equal(10_400_000m, PayslipCalculator.Calculate(FullTime(workDays: 26)).NetPay);

    [Fact]
    public void Half_work_units_are_paid_proportionally() =>
        Assert.Equal(4_000_000m, PayslipCalculator.Calculate(FullTime(workDays: 10)).BasicPay); // 10.400.000 × 10 ÷ 26

    [Fact]
    public void Paid_leave_counts_like_work_days_for_full_time()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 18, paidLeave: 2));

        Assert.Equal(8_000_000m, result.BasicPay); // (18 + 2) công
        Assert.Equal(new[] { "Lương theo công", "Nghỉ phép có lương" }, result.Lines.Select(l => l.Name));
        Assert.Equal(800_000m, result.Lines[1].Amount);
    }

    [Fact]
    public void Basic_pay_is_rounded_from_the_unrounded_value()
    {
        // 9.000.000 × 13 ÷ 26 = 4.500.000 đúng; 9.000.000 × 7 ÷ 26 = 2.423.076,92 → 2.423.077
        Assert.Equal(2_423_077m, PayslipCalculator.Calculate(FullTime(workDays: 7, salary: 9_000_000m)).BasicPay);
    }

    [Fact]
    public void Part_time_pay_is_hours_times_the_position_rate()
    {
        var result = PayslipCalculator.Calculate(PartTime(hours: 80));

        Assert.Equal(2_800_000m, result.BasicPay); // 80 × 35.000
        Assert.Equal(35000m, result.HourlyRate);
        Assert.Equal("Lương theo giờ", Assert.Single(result.Lines).Name);
    }

    [Fact]
    public void Part_time_pay_prefers_the_employee_hourly_override() =>
        Assert.Equal(3_600_000m, PayslipCalculator.Calculate(PartTime(hours: 80, hourly: 45000m)).BasicPay);

    [Fact]
    public void Part_time_paid_leave_is_converted_to_eight_hours_per_work_unit()
    {
        var result = PayslipCalculator.Calculate(PartTime(hours: 40, paidLeave: 1));

        Assert.Equal(1_400_000m + 280_000m, result.BasicPay); // 40 × 35.000 + 8 × 35.000
        Assert.Equal(8m, result.Lines[1].Quantity);
    }

    [Fact]
    public void Overtime_uses_the_full_time_hourly_equivalent_times_the_multiplier()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 26, ot: 4));

        // 10.400.000 ÷ 26 ÷ 8 = 50.000 đ/giờ × 1,5 × 4 giờ = 300.000
        Assert.Equal(300_000m, result.OvertimePay);
        var line = result.Lines.Single(l => l.Kind == PayslipItemKind.Overtime);
        Assert.Equal((4m, 75000m, 300000m), (line.Quantity, line.UnitAmount, line.Amount));
        Assert.Equal(10_700_000m, result.NetPay);
    }

    [Fact]
    public void Part_time_overtime_uses_the_hourly_rate() =>
        Assert.Equal(315_000m, PayslipCalculator.Calculate(PartTime(hours: 0, ot: 6)).OvertimePay); // 6 × 35.000 × 1,5

    [Fact]
    public void Allowances_follow_their_basis_and_zero_amounts_are_hidden()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 26, nights: 5, splits: 0, allowances: new[] { Night, Split, Lunch }));

        Assert.Equal(650_000m, result.AllowanceTotal); // 5 × 30.000 + 0 × 20.000 + 500.000
        Assert.Equal(new[] { "Phụ cấp ca đêm", "Phụ cấp ăn trưa" }, result.Lines.Where(l => l.Kind == PayslipItemKind.Allowance).Select(l => l.Name));
        Assert.Equal(11_050_000m, result.NetPay);
    }

    [Fact]
    public void Penalties_are_deducted_and_shown_as_a_negative_line()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 26, penalty: 70000));

        Assert.Equal(70_000m, result.PenaltyTotal);
        Assert.Equal(10_330_000m, result.NetPay);
        var line = result.Lines.Single(l => l.Kind == PayslipItemKind.Penalty);
        Assert.Equal((2m, -70000m), (line.Quantity, line.Amount));
        Assert.Contains("30 phút", line.Note);
    }

    [Fact]
    public void Net_pay_combines_every_component()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 20, paidLeave: 2, ot: 2, penalty: 50000, nights: 4, allowances: new[] { Night, Lunch }));

        // 8.800.000 (22 công) + 620.000 phụ cấp + 150.000 OT − 50.000 phạt
        Assert.Equal((8_800_000m, 620_000m, 150_000m, 50_000m, 9_520_000m),
            (result.BasicPay, result.AllowanceTotal, result.OvertimePay, result.PenaltyTotal, result.NetPay));
        Assert.Equal(result.Lines.Sum(l => l.Amount), result.NetPay); // các dòng cộng lại đúng thực nhận
    }

    [Fact]
    public void A_month_without_work_pays_only_allowances_that_have_no_quantity_dependency()
    {
        var result = PayslipCalculator.Calculate(FullTime(workDays: 0, allowances: new[] { Night, Lunch }));

        Assert.Equal(500_000m, result.NetPay);
        Assert.Equal(0m, result.BasicPay);
    }

    [Fact]
    public void Missing_salary_or_standard_days_never_divides_by_zero()
    {
        var noSalary = PayslipCalculator.Calculate(FullTime(workDays: 10) with { BaseSalary = null });
        var noStandard = PayslipCalculator.Calculate(FullTime(workDays: 10) with { StandardWorkDays = 0 });

        Assert.Equal(0m, noSalary.NetPay);
        Assert.Equal(4_000_000m * 26m, noStandard.NetPay); // công chuẩn 0 coi như 1: tránh chia cho 0
    }
}
