using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces.Persistence;

namespace FW.Application.Features.Timesheets;

/// <summary>
/// Chặn thay đổi dữ liệu nguồn của tháng đã khóa: sau khi HR khóa bảng công, không duyệt thêm chấm công / đơn ảnh hưởng tới số liệu của
/// tháng đó (muốn sửa phải mở lại bảng công trước).
/// </summary>
public interface ITimesheetGuard
{
    /// <exception cref="BadRequestException">Bảng công của nhân viên trong một tháng thuộc khoảng ngày đã khóa / chốt lương.</exception>
    Task EnsureEditableAsync(int userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public class TimesheetGuard : ITimesheetGuard
{
    private readonly ITimesheetRepository _timesheets;

    public TimesheetGuard(ITimesheetRepository timesheets)
    {
        _timesheets = timesheets;
    }

    public async Task EnsureEditableAsync(int userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        for (var month = new DateTime(fromDate.Year, fromDate.Month, 1); month <= toDate.Date; month = month.AddMonths(1))
        {
            if (await _timesheets.IsFrozenAsync(userId, month.Year, month.Month, cancellationToken))
            {
                throw new BadRequestException(
                    $"Bảng công tháng {month.Month:00}/{month.Year} của nhân viên này đã khóa — HR cần mở lại bảng công trước khi duyệt.");
            }
        }
    }
}
