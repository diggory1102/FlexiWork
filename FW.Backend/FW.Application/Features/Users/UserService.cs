using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Users;

public interface IUserService
{
    Task<PaginatedResult<UserDto>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default);
    Task<UserDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CreateUserResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateAsync(int id, UserProfileRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateStatusAsync(int id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateRolesAsync(int id, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cấp lại mật khẩu tạm và gửi email. Trả về true nếu gửi email thành công.</summary>
    Task<bool> ReissuePasswordAsync(int id, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quản lý hồ sơ nhân sự & tài khoản: tạo mới kèm email mời (mật khẩu tạm), phân vai trò,
/// gán chi nhánh/phòng ban/chức vụ, khóa/mở tài khoản.
/// Phạm vi: Admin/HR toàn công ty (ghi), Manager chỉ xem nhân sự chi nhánh mình.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IBranchRepository _branches;
    private readonly IDepartmentRepository _departments;
    private readonly IPositionRepository _positions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _email;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository users,
        IRoleRepository roles,
        IBranchRepository branches,
        IDepartmentRepository departments,
        IPositionRepository positions,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IEmailService email,
        ICurrentUserService currentUser,
        ILogger<UserService> logger)
    {
        _users = users;
        _roles = roles;
        _branches = branches;
        _departments = departments;
        _positions = positions;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _email = email;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Truy vấn
    // ------------------------------------------------------------------

    public async Task<PaginatedResult<UserDto>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default)
    {
        var scope = _currentUser.GetBranchScope();
        if (scope.HasValue) filter.BranchId = scope;

        var page = await _users.GetPagedAsync(filter, cancellationToken);
        var dtos = await ToDtosAsync(page.Items, cancellationToken);
        return PaginatedResult<UserDto>.Create(dtos, page.TotalCount, page.PageIndex, page.PageSize);
    }

    public async Task<UserDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await GetExistingAsync(id, cancellationToken);

        // Ai cũng xem được hồ sơ của chính mình; xem người khác thì theo phạm vi chi nhánh
        if (id != _currentUser.UserId && !_currentUser.HasCompanyWideAccess())
        {
            _currentUser.EnsureCanAccessBranch(user.BranchId);
        }
        return (await ToDtosAsync(new[] { user }, cancellationToken))[0];
    }

    // ------------------------------------------------------------------
    // Tạo mới & cập nhật
    // ------------------------------------------------------------------

    public async Task<CreateUserResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var roleNames = NormalizeRoles(request.Roles, defaultToEmployee: true);
        UserManagementRules.EnsureCanAssign(_currentUser, roleNames);

        var user = new User();
        await ApplyProfileAsync(user, request, excludeId: null, cancellationToken);
        EnsureManagerHasBranch(roleNames, user.BranchId);

        var roles = await ResolveRolesAsync(roleNames, cancellationToken);
        var temporaryPassword = PasswordGenerator.Generate();
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.Status = UserStatus.Active;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _users.AddAsync(user, ct);
            await _users.SetRolesAsync(user.Id, roles.Select(r => r.Id), ct);
        }, cancellationToken);

        // Gửi email sau khi đã lưu: lỗi SMTP không làm mất tài khoản, HR có thể "cấp lại mật khẩu"
        var sent = await TrySendAsync(user, "[FlexiWork] Thông tin tài khoản của bạn",
            UserEmailTemplates.Invitation(user.FullName, user.Email, temporaryPassword), cancellationToken);

        return new CreateUserResult(await GetByIdAsync(user.Id, cancellationToken), sent);
    }

    public async Task<UserDto> UpdateAsync(int id, UserProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetManageableAsync(id, cancellationToken);
        await ApplyProfileAsync(user, request, excludeId: id, cancellationToken);

        var roles = await _users.GetRoleNamesAsync(id, cancellationToken);
        EnsureManagerHasBranch(roles, user.BranchId);

        await _users.UpdateAsync(user, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDto> UpdateStatusAsync(int id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status.Trim().ToLowerInvariant();
        if (!UserStatus.IsValid(status))
        {
            throw new BadRequestException($"Trạng thái không hợp lệ. Giá trị cho phép: {string.Join(", ", UserStatus.All)}.");
        }

        UserManagementRules.EnsureNotSelf(_currentUser, id, "thay đổi trạng thái");
        var user = await GetManageableAsync(id, cancellationToken);

        user.Status = status;
        await _users.UpdateAsync(user, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDto> UpdateRolesAsync(int id, UpdateUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var roleNames = NormalizeRoles(request.Roles, defaultToEmployee: false);
        if (roleNames.Count == 0) throw new BadRequestException("Người dùng phải có ít nhất một vai trò.");

        UserManagementRules.EnsureNotSelf(_currentUser, id, "thay đổi vai trò của");
        UserManagementRules.EnsureCanAssign(_currentUser, roleNames);
        var user = await GetManageableAsync(id, cancellationToken);
        EnsureManagerHasBranch(roleNames, user.BranchId);

        var roles = await ResolveRolesAsync(roleNames, cancellationToken);
        await _users.SetRolesAsync(id, roles.Select(r => r.Id), cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> ReissuePasswordAsync(int id, CancellationToken cancellationToken = default)
    {
        UserManagementRules.EnsureNotSelf(_currentUser, id, "cấp lại mật khẩu cho");
        var user = await GetManageableAsync(id, cancellationToken);

        var temporaryPassword = PasswordGenerator.Generate();
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        await _users.UpdateAsync(user, cancellationToken);

        return await TrySendAsync(user, "[FlexiWork] Cấp lại mật khẩu",
            UserEmailTemplates.PasswordReissued(user.FullName, user.Email, temporaryPassword), cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        UserManagementRules.EnsureNotSelf(_currentUser, id, "xóa");
        await GetManageableAsync(id, cancellationToken);
        await _users.DeleteAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Hàm hỗ trợ
    // ------------------------------------------------------------------

    private async Task<User> GetExistingAsync(int id, CancellationToken cancellationToken) =>
        await _users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Nhân sự", id);

    /// <summary>Lấy tài khoản mà người dùng hiện tại được phép chỉnh sửa (HR không sửa được Admin).</summary>
    private async Task<User> GetManageableAsync(int id, CancellationToken cancellationToken)
    {
        var user = await GetExistingAsync(id, cancellationToken);
        var roles = await _users.GetRoleNamesAsync(id, cancellationToken);
        UserManagementRules.EnsureCanManage(_currentUser, id, roles);
        return user;
    }

    private async Task ApplyProfileAsync(User user, UserProfileRequest request, int? excludeId, CancellationToken cancellationToken)
    {
        var code = request.EmployeeCode.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var employmentType = string.IsNullOrWhiteSpace(request.EmploymentType)
            ? EmploymentType.FullTime
            : request.EmploymentType.Trim().ToLowerInvariant();

        var errors = new List<string>();
        if (!EmploymentType.IsValid(employmentType))
            errors.Add($"Loại hợp đồng không hợp lệ. Giá trị cho phép: {string.Join(", ", EmploymentType.All)}.");
        if (await _users.ExistsByEmployeeCodeAsync(code, excludeId, cancellationToken))
            errors.Add($"Mã nhân viên '{code}' đã tồn tại.");
        if (await _users.ExistsByEmailAsync(email, excludeId, cancellationToken))
            errors.Add($"Email '{email}' đã được sử dụng.");
        if (request.BranchId.HasValue && !await _branches.ExistsAsync(request.BranchId.Value, cancellationToken))
            errors.Add($"Chi nhánh #{request.BranchId} không tồn tại.");
        if (request.DepartmentId.HasValue && !await _departments.ExistsAsync(request.DepartmentId.Value, cancellationToken))
            errors.Add($"Phòng ban #{request.DepartmentId} không tồn tại.");
        if (request.PositionId.HasValue && !await _positions.ExistsAsync(request.PositionId.Value, cancellationToken))
            errors.Add($"Chức vụ #{request.PositionId} không tồn tại.");

        if (errors.Count > 0) throw new BadRequestException("Thông tin nhân sự không hợp lệ.", errors);

        user.EmployeeCode = code;
        user.Email = email;
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.BranchId = request.BranchId;
        user.DepartmentId = request.DepartmentId;
        user.PositionId = request.PositionId;
        user.EmploymentType = employmentType;
        user.HireDate = request.HireDate?.ToDateTime(TimeOnly.MinValue);
    }

    private static List<string> NormalizeRoles(IEnumerable<string>? roles, bool defaultToEmployee)
    {
        var normalized = (roles ?? Enumerable.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (normalized.Count == 0 && defaultToEmployee) normalized.Add(RoleNames.Employee);

        var invalid = normalized.Where(r => !RoleNames.IsValid(r)).ToList();
        if (invalid.Count > 0)
        {
            throw new BadRequestException($"Vai trò không hợp lệ: {string.Join(", ", invalid)}.");
        }
        return normalized;
    }

    private async Task<IReadOnlyList<Role>> ResolveRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        var roles = await _roles.GetByNamesAsync(roleNames, cancellationToken);
        if (roles.Count != roleNames.Count)
        {
            throw new BadRequestException("Một số vai trò chưa được khởi tạo trong CSDL (bảng roles).");
        }
        return roles;
    }

    /// <summary>Manager được phân quyền theo chi nhánh nên bắt buộc phải thuộc một chi nhánh.</summary>
    private static void EnsureManagerHasBranch(IEnumerable<string> roles, int? branchId)
    {
        if (roles.Contains(RoleNames.Manager) && !branchId.HasValue)
        {
            throw new BadRequestException("Quản lý chi nhánh (ROLE_MANAGER) phải được gán vào một chi nhánh.");
        }
    }

    private async Task<bool> TrySendAsync(User user, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            await _email.SendAsync(user.Email, subject, body, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Gửi email tới {Email} thất bại", user.Email);
            return false;
        }
    }

    /// <summary>Ghép tên chi nhánh/phòng ban/chức vụ và vai trò (danh mục nhỏ nên nạp một lần).</summary>
    private async Task<IReadOnlyList<UserDto>> ToDtosAsync(IReadOnlyList<User> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0) return Array.Empty<UserDto>();

        var branches = (await _branches.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        var departments = (await _departments.GetAllAsync(cancellationToken)).ToDictionary(d => d.Id, d => d.Name);
        var positions = (await _positions.GetAllAsync(cancellationToken)).ToDictionary(p => p.Id, p => p.Title);
        var roles = await _users.GetRoleNamesByUserIdsAsync(users.Select(u => u.Id), cancellationToken);

        return users.Select(u => new UserDto(
            u.Id, u.EmployeeCode, u.Email, u.FullName, u.PhoneNumber, u.AvatarUrl,
            u.BranchId, Lookup(branches, u.BranchId),
            u.DepartmentId, Lookup(departments, u.DepartmentId),
            u.PositionId, Lookup(positions, u.PositionId),
            u.EmploymentType,
            u.HireDate.HasValue ? DateOnly.FromDateTime(u.HireDate.Value) : null,
            u.Status, u.MustChangePassword,
            roles.GetValueOrDefault(u.Id) ?? Array.Empty<string>(),
            u.CreatedAt, u.UpdatedAt)).ToList();
    }

    private static string? Lookup(Dictionary<int, string> map, int? id) =>
        id.HasValue && map.TryGetValue(id.Value, out var name) ? name : null;
}
