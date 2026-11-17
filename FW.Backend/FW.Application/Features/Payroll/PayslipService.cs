using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Domain.Enums;

namespace FW.Application.Features.Payroll;

public interface IPayslipService
{
    /// <summary>Phiếu lương của tôi (UC 8.5): chỉ các kỳ đã chốt, mới nhất trước.</summary>
    Task<PaginatedResult<PayslipDto>> GetMineAsync(MyPayslipFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết phiếu lương kèm từng khoản: chính chủ (kỳ đã chốt) hoặc Admin / HR.</summary>
    Task<PayslipDetailDto> GetDetailAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xem phiếu lương. Nhân viên chỉ thấy phiếu của mình khi kỳ lương đã chốt (phiếu nháp chưa chắc chắn nên không lộ ra); Admin / HR thấy mọi phiếu.
/// Phiếu của người khác hoặc của kỳ chưa chốt đều trả "không tìm thấy" để không lộ việc có tồn tại.
/// </summary>
public class PayslipService : IPayslipService
{
    private readonly IPayslipRepository _payslips;
    private readonly ICurrentUserService _currentUser;

    public PayslipService(IPayslipRepository payslips, ICurrentUserService currentUser)
    {
        _payslips = payslips;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<PayslipDto>> GetMineAsync(MyPayslipFilter filter, CancellationToken cancellationToken = default)
    {
        var page = await _payslips.GetDetailsPagedAsync(new PayslipQuery
        {
            UserId = _currentUser.UserId ?? throw new UnauthorizedException(),
            Year = filter.Year,
            FinalizedOnly = true,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        }, cancellationToken);
        return page.Map(PayslipDto.From);
    }

    public async Task<PayslipDetailDto> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var me = _currentUser.UserId ?? throw new UnauthorizedException();
        var row = await _payslips.GetDetailByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Phiếu lương", id);

        var visible = _currentUser.HasCompanyWideAccess() || (row.UserId == me && row.PeriodStatus == PayrollPeriodStatus.Finalized);
        if (!visible) throw new NotFoundException("Phiếu lương", id);

        var items = await _payslips.GetItemsAsync(id, cancellationToken);
        return new PayslipDetailDto(PayslipDto.From(row), items.Select(PayslipItemDto.From).ToList());
    }
}
