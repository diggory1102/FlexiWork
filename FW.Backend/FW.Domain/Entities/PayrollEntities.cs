using FW.Domain.Common;
using FW.Domain.Enums;

namespace FW.Domain.Entities;

/// <summary>Danh mục phụ cấp (bảng allowances) — HR cấu hình, tính theo <see cref="AllowanceBasis"/>.</summary>
public class Allowance : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Giá trị thuộc <see cref="AllowanceBasis"/>.</summary>
    public string Basis { get; set; } = AllowanceBasis.Monthly;

    /// <summary>Mức tiền mặc định (VNĐ) cho một đơn vị của <see cref="Basis"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>true: mọi nhân viên đều được hưởng (VD phụ cấp ca đêm); false: chỉ người được gán.</summary>
    public bool ApplyToAll { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

/// <summary>Phụ cấp gán riêng cho nhân viên (bảng user_allowances), có thể ghi đè mức tiền.</summary>
public class UserAllowance : BaseEntity, IAuditableEntity
{
    public int UserId { get; set; }
    public int AllowanceId { get; set; }

    /// <summary>Mức tiền riêng; null = dùng mức của danh mục.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Kỳ lương tháng (bảng payroll_periods) — Task 5.5 tính và chốt.</summary>
public class PayrollPeriod : BaseEntity, IAuditableEntity
{
    public int Month { get; set; }
    public int Year { get; set; }

    /// <summary>Giá trị thuộc <see cref="PayrollPeriodStatus"/>.</summary>
    public string Status { get; set; } = PayrollPeriodStatus.Draft;

    /// <summary>Số công chuẩn và hệ số OT tại lúc tính (giữ nguyên dù sau đó HR đổi cấu hình).</summary>
    public decimal StandardWorkDays { get; set; }
    public decimal OvertimeMultiplier { get; set; }

    public int EmployeeCount { get; set; }
    public decimal TotalNet { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? FinalizedBy { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Phiếu lương của một nhân viên trong kỳ (bảng payslips); số liệu là ảnh chụp tại lúc tính.</summary>
public class Payslip : BaseEntity<long>, IAuditableEntity
{
    public int PeriodId { get; set; }
    public int UserId { get; set; }
    public int? BranchId { get; set; }
    public long? TimesheetId { get; set; }

    public string EmploymentType { get; set; } = Enums.EmploymentType.FullTime;
    public decimal BaseSalary { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal StandardWorkDays { get; set; }
    public decimal WorkDays { get; set; }
    public decimal WorkHours { get; set; }
    public decimal OvertimeHours { get; set; }

    public decimal BasicPay { get; set; }
    public decimal AllowanceTotal { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal PenaltyTotal { get; set; }
    public decimal NetPay { get; set; }

    /// <summary>Giá trị thuộc <see cref="PayslipStatus"/>.</summary>
    public string Status { get; set; } = PayslipStatus.Draft;

    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Một dòng của phiếu lương (bảng payslip_items): lương cơ bản, từng phụ cấp, OT, phạt, điều chỉnh.</summary>
public class PayslipItem : BaseEntity<long>
{
    public long PayslipId { get; set; }

    /// <summary>Giá trị thuộc <see cref="PayslipItemKind"/>.</summary>
    public string Kind { get; set; } = PayslipItemKind.Basic;

    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitAmount { get; set; }

    /// <summary>Thành tiền; khoản trừ (phạt) là số âm.</summary>
    public decimal Amount { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
