using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Application.Features.Setup;

/// <summary>
/// Tài khoản Quản trị viên đầu tiên khi triển khai (section "Bootstrap"), lấy từ biến môi trường
/// thay vì seed mật khẩu cố định trong mã nguồn công khai.
/// </summary>
public class BootstrapSettings
{
    public const string SectionName = "Bootstrap";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = "Quản trị viên";
    public string AdminEmployeeCode { get; set; } = "ADMIN";
}

public interface IBootstrapAdminService
{
    /// <summary>Tạo Quản trị viên nếu hệ thống chưa có ai mang vai trò Admin. Trả về true nếu vừa tạo.</summary>
    Task<bool> EnsureAdminAsync(CancellationToken cancellationToken = default);
}

public class BootstrapAdminService : IBootstrapAdminService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly BootstrapSettings _settings;
    private readonly ILogger<BootstrapAdminService> _logger;

    public BootstrapAdminService(
        IUserRepository users,
        IRoleRepository roles,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IOptions<BootstrapSettings> settings,
        ILogger<BootstrapAdminService> logger)
    {
        _users = users;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.AdminEmail) || string.IsNullOrWhiteSpace(_settings.AdminPassword))
        {
            return false; // Không cấu hình → bỏ qua (môi trường dev dùng dữ liệu seed)
        }

        var admins = await _users.GetPagedAsync(new UserFilter { RoleName = RoleNames.Admin, PageSize = 1 }, cancellationToken);
        if (admins.TotalCount > 0)
        {
            return false;
        }

        var passwordErrors = PasswordPolicy.Validate(_settings.AdminPassword);
        if (passwordErrors.Count > 0)
        {
            throw new InvalidOperationException("Bootstrap:AdminPassword không đạt quy tắc mật khẩu: " + string.Join(" ", passwordErrors));
        }

        var email = _settings.AdminEmail.Trim().ToLowerInvariant();
        if (await _users.ExistsByEmailAsync(email, cancellationToken: cancellationToken))
        {
            _logger.LogWarning("Không tạo Quản trị viên đầu tiên: email {Email} đã thuộc về một tài khoản khác.", email);
            return false;
        }

        var adminRole = await _roles.GetByNameAsync(RoleNames.Admin, cancellationToken)
            ?? throw new InvalidOperationException("Bảng roles chưa có ROLE_ADMIN — hãy chạy script seed vai trò trước.");

        var user = new User
        {
            EmployeeCode = _settings.AdminEmployeeCode.Trim().ToUpperInvariant(),
            Email = email,
            FullName = _settings.AdminFullName.Trim(),
            PasswordHash = _passwordHasher.Hash(_settings.AdminPassword),
            EmploymentType = EmploymentType.FullTime,
            Status = UserStatus.Active,
            MustChangePassword = false,
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _users.AddAsync(user, ct);
            await _users.SetRolesAsync(user.Id, new[] { adminRole.Id }, ct);
        }, cancellationToken);

        _logger.LogInformation("Đã tạo tài khoản Quản trị viên đầu tiên: {Email}", email);
        return true;
    }
}
