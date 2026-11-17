using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Requests;

public interface ILeaveTypeService
{
    /// <summary>Danh sách loại đơn (theo nhóm → thứ tự khai báo); <paramref name="activeOnly"/> = chỉ loại đang nhận đơn mới (màn hình tạo đơn).</summary>
    Task<IReadOnlyList<LeaveTypeDto>> GetListAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<LeaveTypeDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> CreateAsync(LeaveTypeRequest request, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> UpdateAsync(int id, LeaveTypeRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Danh mục loại đơn (UC 1.4) — Admin/HR cấu hình (giới hạn quyền ở Controller). Tên không trùng; chỉ nhóm nghỉ được trừ phép năm;
/// loại đã có đơn thì không đổi nhóm được (tạo loại mới thay thế); còn đơn đang xử lý thì chưa xóa được — tạm ngừng (IsActive = false) thay vì xóa.
/// </summary>
public class LeaveTypeService : ILeaveTypeService
{
    private readonly ILeaveTypeRepository _types;

    public LeaveTypeService(ILeaveTypeRepository types)
    {
        _types = types;
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> GetListAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        (await _types.GetListAsync(activeOnly, cancellationToken)).Select(LeaveTypeDto.From).ToList();

    public async Task<LeaveTypeDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        LeaveTypeDto.From(await GetExistingAsync(id, cancellationToken));

    public async Task<LeaveTypeDto> CreateAsync(LeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        var type = new LeaveType();
        await ApplyAsync(type, request, null, cancellationToken);
        await _types.AddAsync(type, cancellationToken);
        return await GetByIdAsync(type.Id, cancellationToken);
    }

    public async Task<LeaveTypeDto> UpdateAsync(int id, LeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        var type = await GetExistingAsync(id, cancellationToken);
        var category = request.Category.Trim();
        if (category != type.Category && (await _types.CountRequestsAsync(id, cancellationToken)).Total > 0)
        {
            throw new BadRequestException("Loại đơn đã có đơn sử dụng nên không đổi nhóm được — hãy tạo loại đơn mới.");
        }

        await ApplyAsync(type, request, id, cancellationToken);
        await _types.UpdateAsync(type, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await GetExistingAsync(id, cancellationToken);
        var (_, open) = await _types.CountRequestsAsync(id, cancellationToken);
        if (open > 0)
        {
            throw new BadRequestException(
                $"Còn {open} đơn loại này đang chờ xử lý — hãy xử lý xong hoặc chuyển sang \"Ngừng nhận đơn\" thay vì xóa.");
        }
        await _types.DeleteAsync(id, cancellationToken);
    }

    private async Task ApplyAsync(LeaveType type, LeaveTypeRequest request, int? excludeId, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var category = request.Category.Trim();
        if (name.Length == 0) throw new BadRequestException("Vui lòng nhập tên loại đơn.");
        if (!RequestCategory.IsValid(category))
        {
            throw new BadRequestException("Nhóm đơn không hợp lệ (leave, shift_swap, overtime, explanation).");
        }
        if (request.DeductsBalance && category != RequestCategory.Leave)
        {
            throw new BadRequestException("Chỉ loại đơn nghỉ mới được trừ quỹ phép năm.");
        }
        if (request.DeductsBalance && !request.IsPaid)
        {
            throw new BadRequestException("Loại đơn trừ phép năm phải là nghỉ có hưởng lương.");
        }
        if (await _types.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new BadRequestException($"Đã có loại đơn \"{name}\".");
        }

        type.Name = name;
        type.Category = category;
        type.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        type.IsPaid = request.IsPaid;
        type.DeductsBalance = request.DeductsBalance;
        type.RequiresAttachment = request.RequiresAttachment;
        type.IsActive = request.IsActive;
    }

    private async Task<LeaveType> GetExistingAsync(int id, CancellationToken cancellationToken) =>
        await _types.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Loại đơn", id);
}
