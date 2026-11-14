using System.Text.RegularExpressions;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Settings;
using FW.Application.Features.Auth;
using FW.Application.Features.Users;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Security;
using FW.Infrastructure.Services;
using FW.Infrastructure.Services.Caching;
using FW.Infrastructure.Services.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FW.IntegrationTests.Users;

/// <summary>
/// Quản lý hồ sơ nhân sự trên MySQL thật: tạo tài khoản kèm mật khẩu tạm, phân quyền Admin/HR/Manager,
/// khóa tài khoản, cấp lại mật khẩu, hồ sơ cá nhân & ảnh đại diện.
/// Seed: 1 = Admin (CN 1), 2 = Manager (CN 1), 3 = Employee (CN 1), 4 = HR (không thuộc CN).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class UserServiceTests
{
    private readonly DatabaseFixture _db;

    public UserServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int id, int? branchId, params string[] roles)
        {
            UserId = id;
            BranchId = branchId;
            Roles = roles;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles { get; }
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class CapturingEmail : IEmailService
    {
        public bool Fail { get; set; }
        public List<string> Bodies { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("SMTP không phản hồi");
            Bodies.Add(htmlBody);
            return Task.CompletedTask;
        }

        /// <summary>Lấy mật khẩu tạm trong email gần nhất (nằm trong thẻ font monospace).</summary>
        public string LastTemporaryPassword() =>
            Regex.Match(Bodies.Last(), "font-family:monospace;font-size:17px\">([^<]+)<").Groups[1].Value;
    }

    private static readonly TestUser Admin = new(1, 1, RoleNames.Admin);
    private static readonly TestUser Hr = new(4, null, RoleNames.Hr);
    private static readonly TestUser Manager = new(2, 1, RoleNames.Manager);

    private static UserService Users(DbSession s, ICurrentUserService user, IEmailService email) => new(
        new UserRepository(s), new RoleRepository(s), new BranchRepository(s), new DepartmentRepository(s),
        new PositionRepository(s), s, new BCryptPasswordHasher(), email, user,
        Sessions(s, new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()))), NullLogger<UserService>.Instance);

    private static SessionStampService Sessions(DbSession s, ICacheService cache) =>
        new(new UserRepository(s), cache, new DateTimeProvider());

    private static AuthService Auth(DbSession s)
    {
        var clock = new DateTimeProvider();
        var jwt = Options.Create(new JwtSettings
        {
            Secret = "integration-test-secret-key-at-least-32-chars", Issuer = "i", Audience = "a", ExpiryMinutes = 60
        });
        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
        return new AuthService(new UserRepository(s), new BCryptPasswordHasher(), new JwtTokenGenerator(jwt, clock),
            cache, new CapturingEmail(), clock, Sessions(s, cache), NullLogger<AuthService>.Instance);
    }

    private static CreateUserRequest NewEmployee(string? code = null, int? branchId = 1, params string[] roles)
    {
        code ??= $"T{Guid.NewGuid():N}"[..12];
        return new CreateUserRequest
        {
            EmployeeCode = code.ToLowerInvariant(),
            Email = $"  {code.ToUpperInvariant()}@Test.Local ",
            FullName = " Nhân viên kiểm thử ",
            PhoneNumber = "0987654321",
            BranchId = branchId,
            DepartmentId = 3,
            PositionId = 4,
            EmploymentType = "PART_TIME",
            HireDate = new DateOnly(2026, 11, 1),
            Roles = roles.Length > 0 ? roles.ToList() : null
        };
    }

    [SkippableFact]
    public async Task Hr_creates_employee_who_must_change_temporary_password()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var email = new CapturingEmail();
        var request = NewEmployee();

        var result = await Users(session, Hr, email).CreateAsync(request);
        var user = result.User;

        Assert.True(result.InvitationSent);
        Assert.Equal(request.EmployeeCode.ToUpperInvariant(), user.EmployeeCode);
        Assert.Equal(request.Email.Trim().ToLowerInvariant(), user.Email);
        Assert.Equal("Nhân viên kiểm thử", user.FullName);
        Assert.Equal(new[] { RoleNames.Employee }, user.Roles);
        Assert.Equal(EmploymentType.PartTime, user.EmploymentType);
        Assert.Equal(new DateOnly(2026, 11, 1), user.HireDate);
        Assert.Equal("FlexiWork Cầu Giấy", user.BranchName);
        Assert.Equal("Khối Vận hành Chi nhánh", user.DepartmentName);
        Assert.True(user.MustChangePassword);

        // Đăng nhập bằng mật khẩu tạm trong email → bị yêu cầu đổi → đổi xong thì hết cờ
        var tempPassword = email.LastTemporaryPassword();
        var auth = Auth(session);
        var login = await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = tempPassword });
        Assert.True(login.User.MustChangePassword);

        await auth.ChangePasswordAsync(user.Id, new ChangePasswordRequest { CurrentPassword = tempPassword, NewPassword = "MyOwnPass1" });
        Assert.False((await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "MyOwnPass1" })).User.MustChangePassword);
    }

    [SkippableFact]
    public async Task Create_validates_roles_branch_and_uniqueness()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var hrService = Users(session, Hr, new CapturingEmail());

        await Assert.ThrowsAsync<ForbiddenException>(() => hrService.CreateAsync(NewEmployee(roles: RoleNames.Admin)));
        await Assert.ThrowsAsync<BadRequestException>(() => hrService.CreateAsync(NewEmployee(branchId: null, roles: RoleNames.Manager)));
        await Assert.ThrowsAsync<BadRequestException>(() => hrService.CreateAsync(NewEmployee(roles: "ROLE_SUPERMAN")));

        var duplicate = NewEmployee("EMP001");
        duplicate.Email = "admin@flexiwork.vn";
        duplicate.BranchId = 999;
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => hrService.CreateAsync(duplicate));
        Assert.Equal(3, ex.Errors!.Count); // trùng mã, trùng email, chi nhánh không tồn tại

        // Admin được tạo tài khoản HR
        var hrAccount = await Users(session, Admin, new CapturingEmail()).CreateAsync(NewEmployee(branchId: null, roles: RoleNames.Hr));
        Assert.Equal(new[] { RoleNames.Hr }, hrAccount.User.Roles);
    }

    [SkippableFact]
    public async Task Email_failure_keeps_account_and_reports_it()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();

        var result = await Users(session, Hr, new CapturingEmail { Fail = true }).CreateAsync(NewEmployee());

        Assert.False(result.InvitationSent);
        Assert.NotNull(await new UserRepository(session).GetByIdAsync(result.User.Id));
    }

    [SkippableFact]
    public async Task Manager_sees_only_staff_of_own_branch_but_can_view_self()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Users(session, Manager, new CapturingEmail());

        var page = await service.GetPagedAsync(new UserFilter { BranchId = 2, PageSize = 100 });
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, u => Assert.Equal(1, u.BranchId));

        Assert.Equal("EMP003", (await service.GetByIdAsync(3)).EmployeeCode);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(4)); // HR không thuộc chi nhánh 1
        Assert.Equal("EMP002", (await service.GetByIdAsync(2)).EmployeeCode);   // chính mình
    }

    [SkippableFact]
    public async Task Status_changes_respect_rules_and_block_login()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var email = new CapturingEmail();
        var hrService = Users(session, Hr, email);
        var created = (await hrService.CreateAsync(NewEmployee())).User;
        var tempPassword = email.LastTemporaryPassword();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            hrService.UpdateStatusAsync(1, new UpdateUserStatusRequest { Status = UserStatus.Suspended })); // HR không khóa Admin
        await Assert.ThrowsAsync<BadRequestException>(() =>
            Users(session, Admin, email).UpdateStatusAsync(1, new UpdateUserStatusRequest { Status = UserStatus.Suspended })); // tự khóa
        await Assert.ThrowsAsync<BadRequestException>(() =>
            hrService.UpdateStatusAsync(created.Id, new UpdateUserStatusRequest { Status = "banned" }));

        var locked = await hrService.UpdateStatusAsync(created.Id, new UpdateUserStatusRequest { Status = "SUSPENDED" });
        Assert.Equal(UserStatus.Suspended, locked.Status);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(session).LoginAsync(new LoginRequest { Email = created.Email, Password = tempPassword }));
        Assert.Contains("bị khóa", ex.Message);

        await hrService.UpdateStatusAsync(created.Id, new UpdateUserStatusRequest { Status = UserStatus.Active });
        Assert.NotNull(await Auth(session).LoginAsync(new LoginRequest { Email = created.Email, Password = tempPassword }));
    }

    [SkippableFact]
    public async Task Admin_updates_roles_and_manager_role_requires_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var adminService = Users(session, Admin, new CapturingEmail());
        var noBranch = (await adminService.CreateAsync(NewEmployee(branchId: null))).User;
        var withBranch = (await adminService.CreateAsync(NewEmployee())).User;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            adminService.UpdateRolesAsync(noBranch.Id, new UpdateUserRolesRequest { Roles = new() { RoleNames.Manager } }));

        var promoted = await adminService.UpdateRolesAsync(withBranch.Id,
            new UpdateUserRolesRequest { Roles = new() { "role_manager", RoleNames.Employee } });
        Assert.Equal(new[] { RoleNames.Manager, RoleNames.Employee }, promoted.Roles);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            adminService.UpdateRolesAsync(1, new UpdateUserRolesRequest { Roles = new() { RoleNames.Employee } })); // tự đổi vai trò
    }

    [SkippableFact]
    public async Task Update_profile_and_reissue_password()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var email = new CapturingEmail();
        var hrService = Users(session, Hr, email);
        var created = (await hrService.CreateAsync(NewEmployee())).User;
        var firstPassword = email.LastTemporaryPassword();

        var update = NewEmployee(created.EmployeeCode, branchId: 2);
        update.Email = created.Email;
        update.FullName = "Đã đổi tên";
        update.EmploymentType = null;
        var updated = await hrService.UpdateAsync(created.Id, update);
        Assert.Equal("Đã đổi tên", updated.FullName);
        Assert.Equal("FlexiWork Hoàn Kiếm", updated.BranchName);
        Assert.Equal(EmploymentType.FullTime, updated.EmploymentType);

        Assert.True(await hrService.ReissuePasswordAsync(created.Id));
        var secondPassword = email.LastTemporaryPassword();
        Assert.NotEqual(firstPassword, secondPassword);

        var auth = Auth(session);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequest { Email = created.Email, Password = firstPassword }));
        Assert.True((await auth.LoginAsync(new LoginRequest { Email = created.Email, Password = secondPassword })).User.MustChangePassword);

        await Assert.ThrowsAsync<BadRequestException>(() => hrService.DeleteAsync(4)); // tự xóa
        await hrService.DeleteAsync(created.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => hrService.GetByIdAsync(created.Id));
    }

    [SkippableFact]
    public async Task Employee_updates_own_profile_and_avatar()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var created = (await Users(session, Hr, new CapturingEmail()).CreateAsync(NewEmployee())).User;

        var me = new TestUser(created.Id, 1, RoleNames.Employee);
        var uploadsRoot = Path.Combine(Path.GetTempPath(), "flexiwork-test-uploads", Guid.NewGuid().ToString("N"));
        var profile = new ProfileService(new UserRepository(session), Users(session, me, new CapturingEmail()),
            new LocalFileStorageService(uploadsRoot, "http://localhost:5010"), me);

        Assert.Equal(created.Id, (await profile.GetMyProfileAsync()).Id);
        Assert.Equal("0911222333", (await profile.UpdateMyProfileAsync(new UpdateMyProfileRequest { PhoneNumber = " 0911222333 " })).PhoneNumber);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            profile.UpdateMyAvatarAsync(new MemoryStream(new byte[10]), "a.gif", "image/gif", 10));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            profile.UpdateMyAvatarAsync(new MemoryStream(), "a.png", "image/png", ProfileService.MaxAvatarBytes + 1));

        var withAvatar = await profile.UpdateMyAvatarAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "../../evil.png", "image/png", 3);
        Assert.StartsWith("http://localhost:5010/uploads/avatars/", withAvatar.AvatarUrl);
        var savedFile = Path.Combine(uploadsRoot, "avatars", Path.GetFileName(new Uri(withAvatar.AvatarUrl!).LocalPath));
        Assert.True(File.Exists(savedFile)); // lưu đúng thư mục, không theo đường dẫn "../.." của tên gốc

        Directory.Delete(uploadsRoot, recursive: true);
    }
}
