using Dapper;
using FW.Application.Features.Setup;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace FW.IntegrationTests.Setup;

/// <summary>
/// Tạo Quản trị viên đầu tiên từ biến môi trường khi triển khai lên cloud (CSDL chưa có Admin).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class BootstrapAdminTests
{
    private readonly DatabaseFixture _db;

    public BootstrapAdminTests(DatabaseFixture db)
    {
        _db = db;
    }

    private static BootstrapAdminService CreateService(DbSession session, BootstrapSettings settings) =>
        new(new UserRepository(session), new RoleRepository(session), session, new BCryptPasswordHasher(),
            Options.Create(settings), NullLogger<BootstrapAdminService>.Instance);

    [SkippableFact]
    public async Task Does_nothing_when_not_configured()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();

        Assert.False(await CreateService(session, new BootstrapSettings()).EnsureAdminAsync());
    }

    [SkippableFact]
    public async Task Does_nothing_when_an_admin_already_exists()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var settings = new BootstrapSettings { AdminEmail = "second.admin@test.local", AdminPassword = "Bootstrap123" };

        Assert.False(await CreateService(session, settings).EnsureAdminAsync());
        Assert.Null(await new UserRepository(session).GetByEmailAsync("second.admin@test.local"));
    }

    [SkippableFact]
    public async Task Creates_admin_with_hashed_password_when_none_exists()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var raw = new MySqlConnection(_db.ConnectionString);
        await raw.OpenAsync();

        // Tạm gỡ vai trò Admin của tài khoản seed để mô phỏng CSDL mới trên cloud
        var adminRoleId = await raw.ExecuteScalarAsync<int>("SELECT id FROM roles WHERE role_name = @r", new { r = RoleNames.Admin });
        var seedAdminIds = (await raw.QueryAsync<int>("SELECT user_id FROM user_roles WHERE role_id = @id", new { id = adminRoleId })).ToList();
        await raw.ExecuteAsync("DELETE FROM user_roles WHERE role_id = @id", new { id = adminRoleId });
        const string email = "cloud.admin@test.local";

        try
        {
            await using var session = _db.CreateSession();
            var settings = new BootstrapSettings { AdminEmail = "Cloud.Admin@Test.Local", AdminPassword = "Bootstrap123", AdminFullName = "Admin Cloud" };

            Assert.True(await CreateService(session, settings).EnsureAdminAsync());

            await using var verify = _db.CreateSession();
            var users = new UserRepository(verify);
            var created = await users.GetByEmailAsync(email);
            Assert.NotNull(created);
            Assert.False(created!.MustChangePassword);
            Assert.True(new BCryptPasswordHasher().Verify("Bootstrap123", created.PasswordHash));
            Assert.Equal(new[] { RoleNames.Admin }, await users.GetRoleNamesAsync(created.Id));

            // Lần khởi động sau: đã có Admin → không tạo thêm
            Assert.False(await CreateService(verify, settings).EnsureAdminAsync());
        }
        finally
        {
            var createdId = await raw.ExecuteScalarAsync<int?>("SELECT id FROM users WHERE email = @email", new { email });
            if (createdId is not null)
            {
                await raw.ExecuteAsync("DELETE FROM user_roles WHERE user_id = @id", new { id = createdId });
                await raw.ExecuteAsync("DELETE FROM users WHERE id = @id", new { id = createdId });
            }
            foreach (var userId in seedAdminIds)
            {
                await raw.ExecuteAsync("INSERT INTO user_roles (user_id, role_id) VALUES (@userId, @roleId)", new { userId, roleId = adminRoleId });
            }
        }
    }

    [SkippableFact]
    public async Task Rejects_weak_password()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var raw = new MySqlConnection(_db.ConnectionString);
        await raw.OpenAsync();
        var adminRoleId = await raw.ExecuteScalarAsync<int>("SELECT id FROM roles WHERE role_name = @r", new { r = RoleNames.Admin });
        var seedAdminIds = (await raw.QueryAsync<int>("SELECT user_id FROM user_roles WHERE role_id = @id", new { id = adminRoleId })).ToList();
        await raw.ExecuteAsync("DELETE FROM user_roles WHERE role_id = @id", new { id = adminRoleId });

        try
        {
            await using var session = _db.CreateSession();
            var settings = new BootstrapSettings { AdminEmail = "weak.admin@test.local", AdminPassword = "123" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(session, settings).EnsureAdminAsync());
        }
        finally
        {
            foreach (var userId in seedAdminIds)
            {
                await raw.ExecuteAsync("INSERT INTO user_roles (user_id, role_id) VALUES (@userId, @roleId)", new { userId, roleId = adminRoleId });
            }
        }
    }
}
