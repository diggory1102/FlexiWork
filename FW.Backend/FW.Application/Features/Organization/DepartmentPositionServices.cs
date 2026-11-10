using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Features.Organization;

// =============================================================================
// Phòng ban
// =============================================================================

public interface IDepartmentService
{
    Task<PaginatedResult<DepartmentDto>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<DepartmentDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DepartmentDto> CreateAsync(DepartmentRequest request, CancellationToken cancellationToken = default);
    Task<DepartmentDto> UpdateAsync(int id, DepartmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Danh mục phòng ban — dữ liệu dùng chung toàn doanh nghiệp.</summary>
public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departments;
    private readonly IUserRepository _users;

    public DepartmentService(IDepartmentRepository departments, IUserRepository users)
    {
        _departments = departments;
        _users = users;
    }

    public async Task<PaginatedResult<DepartmentDto>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default) =>
        (await _departments.GetPagedAsync(query, cancellationToken)).Map(DepartmentDto.From);

    public async Task<DepartmentDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        DepartmentDto.From(await GetExistingAsync(id, cancellationToken));

    public async Task<DepartmentDto> CreateAsync(DepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeAvailableAsync(code, null, cancellationToken);

        var department = new Department
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = NullIfEmpty(request.Description)
        };
        await _departments.AddAsync(department, cancellationToken);
        return await GetByIdAsync(department.Id, cancellationToken);
    }

    public async Task<DepartmentDto> UpdateAsync(int id, DepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await GetExistingAsync(id, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeAvailableAsync(code, id, cancellationToken);

        department.Code = code;
        department.Name = request.Name.Trim();
        department.Description = NullIfEmpty(request.Description);

        await _departments.UpdateAsync(department, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await GetExistingAsync(id, cancellationToken);

        var staff = await _users.GetPagedAsync(new UserFilter { DepartmentId = id, PageSize = 1 }, cancellationToken);
        if (staff.TotalCount > 0)
        {
            throw new BadRequestException($"Không thể xóa: phòng ban đang có {staff.TotalCount} nhân sự.");
        }

        await _departments.DeleteAsync(id, cancellationToken);
    }

    private async Task<Department> GetExistingAsync(int id, CancellationToken cancellationToken) =>
        await _departments.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Phòng ban", id);

    private async Task EnsureCodeAvailableAsync(string code, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _departments.ExistsByCodeAsync(code, excludeId, cancellationToken))
        {
            throw new BadRequestException($"Mã phòng ban '{code}' đã tồn tại.");
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// =============================================================================
// Chức vụ & đơn giá công
// =============================================================================

public interface IPositionService
{
    Task<PaginatedResult<PositionDto>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<PositionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PositionDto> CreateAsync(PositionRequest request, CancellationToken cancellationToken = default);
    Task<PositionDto> UpdateAsync(int id, PositionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Danh mục chức danh và đơn giá công theo giờ (dùng để tính lương tạm tính).</summary>
public class PositionService : IPositionService
{
    private readonly IPositionRepository _positions;
    private readonly IUserRepository _users;

    public PositionService(IPositionRepository positions, IUserRepository users)
    {
        _positions = positions;
        _users = users;
    }

    public async Task<PaginatedResult<PositionDto>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default) =>
        (await _positions.GetPagedAsync(query, cancellationToken)).Map(PositionDto.From);

    public async Task<PositionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        PositionDto.From(await GetExistingAsync(id, cancellationToken));

    public async Task<PositionDto> CreateAsync(PositionRequest request, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        await EnsureTitleAvailableAsync(title, null, cancellationToken);

        var position = new Position
        {
            Title = title,
            BaseSalaryPerHour = ValidateSalary(request.BaseSalaryPerHour),
            Description = NullIfEmpty(request.Description)
        };
        await _positions.AddAsync(position, cancellationToken);
        return await GetByIdAsync(position.Id, cancellationToken);
    }

    public async Task<PositionDto> UpdateAsync(int id, PositionRequest request, CancellationToken cancellationToken = default)
    {
        var position = await GetExistingAsync(id, cancellationToken);
        var title = request.Title.Trim();
        await EnsureTitleAvailableAsync(title, id, cancellationToken);

        position.Title = title;
        position.BaseSalaryPerHour = ValidateSalary(request.BaseSalaryPerHour);
        position.Description = NullIfEmpty(request.Description);

        await _positions.UpdateAsync(position, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await GetExistingAsync(id, cancellationToken);

        var staff = await _users.GetPagedAsync(new UserFilter { PositionId = id, PageSize = 1 }, cancellationToken);
        if (staff.TotalCount > 0)
        {
            throw new BadRequestException($"Không thể xóa: đang có {staff.TotalCount} nhân sự giữ chức danh này.");
        }

        await _positions.DeleteAsync(id, cancellationToken);
    }

    private async Task<Position> GetExistingAsync(int id, CancellationToken cancellationToken) =>
        await _positions.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Chức vụ", id);

    private async Task EnsureTitleAvailableAsync(string title, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _positions.ExistsByTitleAsync(title, excludeId, cancellationToken))
        {
            throw new BadRequestException($"Chức danh '{title}' đã tồn tại.");
        }
    }

    private static decimal ValidateSalary(decimal value)
    {
        if (value < 0) throw new BadRequestException("Đơn giá công theo giờ không được âm.");
        return decimal.Round(value, 2);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
