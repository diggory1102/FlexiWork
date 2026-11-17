using FW.Domain.Enums;
using FW.Domain.Services;

namespace FW.UnitTests.Domain;

public class PayrollRulesTests
{
    [Theory]
    [InlineData(45000, 35000, 45000)] // mức riêng thắng chức vụ
    [InlineData(null, 35000, 35000)]  // chưa có mức riêng → theo chức vụ
    [InlineData(null, null, 0)]       // chưa có gì
    public void Effective_hourly_rate_prefers_the_employee_override(int? hourlyOverride, int? positionRate, int expected) =>
        Assert.Equal((decimal)expected, PayrollRules.EffectiveHourlyRate(hourlyOverride, positionRate));

    [Fact]
    public void Full_time_overtime_rate_is_monthly_salary_over_standard_days_and_8_hours()
    {
        // 10.400.000 ÷ 26 công ÷ 8 giờ = 50.000 đ/giờ
        Assert.Equal(50000m, PayrollRules.OvertimeHourlyRate(EmploymentType.FullTime, 10_400_000m, null, 35000m, 26m));
        // Lương tháng thắng đơn giá giờ riêng và chức vụ
        Assert.Equal(50000m, PayrollRules.OvertimeHourlyRate(EmploymentType.FullTime, 10_400_000m, 99000m, 35000m, 26m));
    }

    [Fact]
    public void Full_time_overtime_rate_is_rounded_to_two_decimals()
    {
        // 9.000.000 ÷ 26 ÷ 8 = 43.269,2307… → 43.269,23
        Assert.Equal(43269.23m, PayrollRules.OvertimeHourlyRate(EmploymentType.FullTime, 9_000_000m, null, null, 26m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Full_time_without_salary_falls_back_to_the_hourly_rate(int? salary) =>
        Assert.Equal(40000m, PayrollRules.OvertimeHourlyRate(EmploymentType.FullTime, salary, null, 40000m, 26m));

    [Fact]
    public void Part_time_overtime_rate_is_the_hourly_rate_even_with_a_salary_on_file() =>
        Assert.Equal(45000m, PayrollRules.OvertimeHourlyRate(EmploymentType.PartTime, 8_000_000m, 45000m, 35000m, 26m));

    [Fact]
    public void Zero_standard_days_does_not_divide_by_zero() =>
        Assert.Equal(35000m, PayrollRules.OvertimeHourlyRate(EmploymentType.FullTime, 8_000_000m, null, 35000m, 0m));

    [Theory]
    [InlineData(EmploymentType.FullTime, 9_000_000, 0, true)]
    [InlineData(EmploymentType.FullTime, null, 50000, false)]  // toàn thời gian cần lương tháng, đơn giá giờ không đủ
    [InlineData(EmploymentType.FullTime, 0, 0, false)]
    [InlineData(EmploymentType.PartTime, null, 35000, true)]
    [InlineData(EmploymentType.PartTime, 9_000_000, 0, false)] // bán thời gian cần đơn giá giờ
    public void Configured_depends_on_the_employment_type(string type, int? salary, int hourly, bool expected) =>
        Assert.Equal(expected, PayrollRules.IsConfigured(type, salary, hourly));

    [Theory]
    [InlineData(AllowanceBasis.Monthly, 5, 3, 1)]
    [InlineData(AllowanceBasis.NightShift, 5, 3, 5)]
    [InlineData(AllowanceBasis.SplitDay, 5, 3, 3)]
    [InlineData("unknown", 5, 3, 1)]
    public void Allowance_quantity_follows_the_basis(string basis, int nights, int splits, int expected) =>
        Assert.Equal(expected, PayrollRules.AllowanceQuantity(basis, nights, splits));

    [Fact]
    public void Allowance_total_multiplies_quantity_by_the_amount()
    {
        Assert.Equal(150000m, PayrollRules.AllowanceTotal(AllowanceBasis.NightShift, 30000m, 5, 0));
        Assert.Equal(60000m, PayrollRules.AllowanceTotal(AllowanceBasis.SplitDay, 20000m, 9, 3));
        Assert.Equal(500000m, PayrollRules.AllowanceTotal(AllowanceBasis.Monthly, 500000m, 9, 3));
        Assert.Equal(0m, PayrollRules.AllowanceTotal(AllowanceBasis.NightShift, 30000m, 0, 3)); // không có ca đêm
    }

    [Fact]
    public void The_employee_override_replaces_the_catalog_amount()
    {
        Assert.Equal(40000m, PayrollRules.AllowanceUnitAmount(30000m, 40000m));
        Assert.Equal(30000m, PayrollRules.AllowanceUnitAmount(30000m, null));
    }

    [Theory]
    [InlineData(0.99, false)]
    [InlineData(1, true)]
    [InlineData(1.5, true)]
    [InlineData(5, true)]
    [InlineData(5.01, false)]
    public void Overtime_multiplier_must_be_between_1_and_5(double value, bool expected) =>
        Assert.Equal(expected, PayrollRules.IsValidOvertimeMultiplier((decimal)value));

    [Theory]
    [InlineData(AllowanceBasis.Monthly, true)]
    [InlineData(AllowanceBasis.NightShift, true)]
    [InlineData(AllowanceBasis.SplitDay, true)]
    [InlineData("weekly", false)]
    [InlineData(null, false)]
    public void Only_known_allowance_bases_are_valid(string? basis, bool expected) => Assert.Equal(expected, AllowanceBasis.IsValid(basis));
}
