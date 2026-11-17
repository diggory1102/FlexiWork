using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public class PayrollPeriodRow : PayrollPeriod
{
    public string? CreatedByName { get; set; }
    public string? FinalizedByName { get; set; }
    public int PayslipCount { get; set; }

    /// <summary>Số phiếu lương đã gửi email.</summary>
    public int SentCount { get; set; }
}

public interface IPayrollPeriodRepository : IGenericRepository<PayrollPeriod>
{
    Task<PayrollPeriodRow?> GetRowAsync(int id, CancellationToken cancellationToken = default);

    Task<PayrollPeriodRow?> GetRowByMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Các kỳ lương, mới nhất trước; <paramref name="year"/> null = mọi năm.</summary>
    Task<IReadOnlyList<PayrollPeriodRow>> GetRowsAsync(int? year, CancellationToken cancellationToken = default);

    /// <summary>Lấy kỳ lương của tháng, chưa có thì tạo (an toàn khi hai người cùng tạo một lúc).</summary>
    /// <returns>Mã kỳ lương.</returns>
    Task<int> EnsureAsync(int year, int month, int createdBy, CancellationToken cancellationToken = default);

    /// <summary>Ghi kết quả tính. false nếu kỳ không còn là nháp (vừa được chốt ở phiên khác).</summary>
    Task<bool> TryUpdateCalculationAsync(int id, decimal standardWorkDays, decimal overtimeMultiplier, int employeeCount, decimal totalNet, DateTime calculatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Nháp → đã chốt. false nếu kỳ không còn là nháp.</summary>
    Task<bool> TryFinalizeAsync(int id, int finalizedBy, DateTime finalizedAt, CancellationToken cancellationToken = default);
}

/// <summary>Phiếu lương kèm thông tin nhân viên và kỳ lương.</summary>
public class PayslipRow : Payslip
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PositionTitle { get; set; }
    public string? BranchName { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }

    /// <summary>Trạng thái kỳ lương chứa phiếu này.</summary>
    public string PeriodStatus { get; set; } = string.Empty;
}

public class PayslipQuery : PagedQuery
{
    public int? PeriodId { get; set; }
    public int? UserId { get; set; }
    public int? BranchId { get; set; }
    public int? Year { get; set; }
    public string? Status { get; set; }

    /// <summary>Chỉ phiếu của kỳ đã chốt (nhân viên xem phiếu của mình).</summary>
    public bool FinalizedOnly { get; set; }
}

public record PayslipTotals(int Count, decimal Basic, decimal Allowance, decimal Overtime, decimal Penalty, decimal Net);

public sealed record PayslipWithItems(Payslip Payslip, IReadOnlyList<PayslipItem> Items);

public interface IPayslipRepository : IGenericRepository<Payslip, long>
{
    /// <summary>Thay toàn bộ phiếu lương của kỳ (xóa phiếu cũ cùng các dòng, ghi phiếu mới) — gọi trong transaction.</summary>
    Task ReplaceForPeriodAsync(int periodId, IReadOnlyList<PayslipWithItems> payslips, CancellationToken cancellationToken = default);

    Task<PaginatedResult<PayslipRow>> GetDetailsPagedAsync(PayslipQuery query, CancellationToken cancellationToken = default);

    Task<PayslipRow?> GetDetailByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Phiếu lương của kỳ (kèm email nhân viên); <paramref name="onlyUnsent"/> = chỉ phiếu chưa gửi; <paramref name="ids"/> = chỉ các phiếu này.</summary>
    Task<IReadOnlyList<PayslipRow>> GetRowsByPeriodAsync(int periodId, bool onlyUnsent, IReadOnlyCollection<long>? ids = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayslipItem>> GetItemsAsync(long payslipId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, IReadOnlyList<PayslipItem>>> GetItemsByPayslipsAsync(IReadOnlyCollection<long> payslipIds,
        CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu đã gửi email. <returns>Số phiếu được đánh dấu.</returns></summary>
    Task<int> MarkSentAsync(IReadOnlyCollection<long> ids, DateTime sentAt, CancellationToken cancellationToken = default);

    Task<PayslipTotals> GetTotalsAsync(int periodId, CancellationToken cancellationToken = default);
}
