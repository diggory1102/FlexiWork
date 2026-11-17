namespace FW.Domain.Enums;

/// <summary>
/// Cách tính số lượng của một phụ cấp (cột allowances.basis): tiền phụ cấp = <c>số lượng × mức tiền</c>.
/// </summary>
public static class AllowanceBasis
{
    /// <summary>Cố định theo tháng (ăn trưa, xăng xe...) — số lượng luôn là 1.</summary>
    public const string Monthly = "monthly";

    /// <summary>Theo số ca đêm đã làm trong tháng.</summary>
    public const string NightShift = "night_shift";

    /// <summary>Theo số ngày ca gãy trong tháng.</summary>
    public const string SplitDay = "split_day";

    public static readonly IReadOnlyList<string> All = new[] { Monthly, NightShift, SplitDay };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);

    public static string Label(string basis) => basis switch
    {
        NightShift => "mỗi ca đêm",
        SplitDay => "mỗi ngày ca gãy",
        _ => "mỗi tháng"
    };
}

/// <summary>Trạng thái kỳ lương (cột payroll_periods.status): <c>draft</c> tính lại được → <c>finalized</c> đã chốt.</summary>
public static class PayrollPeriodStatus
{
    public const string Draft = "draft";
    public const string Finalized = "finalized";
}

/// <summary>Trạng thái phiếu lương (cột payslips.status): <c>draft</c> → <c>sent</c> đã gửi cho nhân viên.</summary>
public static class PayslipStatus
{
    public const string Draft = "draft";
    public const string Sent = "sent";
}

/// <summary>Loại dòng trong phiếu lương (cột payslip_items.kind); số tiền khoản trừ ghi số âm.</summary>
public static class PayslipItemKind
{
    public const string Basic = "basic";
    public const string Allowance = "allowance";
    public const string Overtime = "overtime";
    public const string Penalty = "penalty";
    public const string Adjustment = "adjustment";
}
