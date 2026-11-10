using Dapper;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Services;

namespace FW.IntegrationTests.Repositories;

/// <summary>
/// Kiểm thử BaseRepository, các repository Master Data, DbSession (transaction) và múi giờ trên MySQL thật.
/// Mỗi test tự tạo dữ liệu với mã duy nhất để không phụ thuộc lẫn nhau.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RepositoryTests
{
    private readonly DatabaseFixture _db;
    private readonly DateTimeProvider _clock = new();

    public RepositoryTests(DatabaseFixture db)
    {
        _db = db;
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private void SkipIfUnavailable() => Skip.IfNot(_db.IsAvailable, _db.SkipReason);

    [SkippableFact]
    public async Task Connection_uses_vietnam_time_zone()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var connection = await session.GetConnectionAsync();

        var dbNow = await connection.ExecuteScalarAsync<DateTime>("SELECT NOW()");

        Assert.True(Math.Abs((dbNow - _clock.Now).TotalMinutes) < 2,
            $"NOW() của MySQL = {dbNow:O}, giờ Việt Nam của ứng dụng = {_clock.Now:O}");
    }

    [SkippableFact]
    public async Task Branch_crud_round_trip_with_soft_delete()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var repo = new BranchRepository(session);
        var code = Unique("T-");

        var branch = new Branch
        {
            BranchCode = code,
            Name = "Chi nhánh kiểm thử",
            Address = "Hà Nội",
            Latitude = 21.03158400m,
            Longitude = 105.78311500m,
            Radius = 75
        };

        var id = await repo.AddAsync(branch);
        Assert.True(id > 0);
        Assert.Equal(id, branch.Id);

        var saved = await repo.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(code, saved!.BranchCode);
        Assert.Equal(21.03158400m, saved.Latitude);
        Assert.Equal(105.78311500m, saved.Longitude);
        Assert.Equal(BranchStatus.Active, saved.Status);
        Assert.False(saved.IsDeleted);
        Assert.True(Math.Abs((saved.CreatedAt - _clock.Now).TotalMinutes) < 2, "created_at phải theo giờ Việt Nam");
        Assert.Null(saved.UpdatedAt);

        saved.Name = "Đã đổi tên";
        Assert.True(await repo.UpdateAsync(saved));
        var updated = await repo.GetByIdAsync(id);
        Assert.Equal("Đã đổi tên", updated!.Name);
        Assert.NotNull(updated.UpdatedAt);

        Assert.True(await repo.DeleteAsync(id));
        Assert.Null(await repo.GetByIdAsync(id));
        Assert.False(await repo.ExistsAsync(id));
        Assert.DoesNotContain(await repo.GetAllAsync(), b => b.Id == id);
        Assert.False(await repo.DeleteAsync(id));     // xóa lần 2 không còn tác dụng
        Assert.False(await repo.UpdateAsync(saved));  // không sửa được bản ghi đã xóa

        // branch_code là UNIQUE trên toàn bảng nên vẫn tính là trùng sau khi xóa mềm
        Assert.True(await repo.ExistsByCodeAsync(code));
        Assert.False(await repo.ExistsByCodeAsync(code, excludeId: id));
    }

    [SkippableFact]
    public async Task Paged_search_matches_literally_and_reports_metadata()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var repo = new DepartmentRepository(session);
        var marker = Unique("PB");

        for (var i = 1; i <= 3; i++)
        {
            await repo.AddAsync(new Department { Code = $"{marker[..15]}-{i}", Name = $"Phòng {marker} {i}" });
        }

        var page = await repo.GetPagedAsync(new PagedQuery { Search = marker, PageIndex = 2, PageSize = 2 });
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);

        // Ký tự đặc biệt của LIKE được tìm theo nghĩa đen, không phải ký tự đại diện
        var wildcard = await repo.GetPagedAsync(new PagedQuery { Search = "%" });
        Assert.Equal(0, wildcard.TotalCount);
    }

    [SkippableFact]
    public async Task Paged_query_normalizes_invalid_paging_values()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var page = await new PositionRepository(session).GetPagedAsync(new PagedQuery { PageIndex = 0, PageSize = 1000 });

        Assert.Equal(1, page.PageIndex);
        Assert.Equal(PagedQuery.MaxPageSize, page.PageSize);
    }

    [SkippableFact]
    public async Task Roles_are_seeded_including_hr()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var repo = new RoleRepository(session);

        var roles = await repo.GetByNamesAsync(RoleNames.All);

        Assert.Equal(RoleNames.All.OrderBy(r => r), roles.Select(r => r.RoleName).OrderBy(r => r));
        Assert.NotNull(await repo.GetByNameAsync(RoleNames.Hr));
    }

    [SkippableFact]
    public async Task User_lookup_and_filters_work_on_seed_data()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var repo = new UserRepository(session);

        var admin = await repo.GetByEmailAsync("admin@flexiwork.vn");
        Assert.NotNull(admin);
        Assert.Equal("EMP001", admin!.EmployeeCode);
        Assert.Equal(new[] { RoleNames.Admin }, await repo.GetRoleNamesAsync(admin.Id));

        var managers = await repo.GetPagedAsync(new UserFilter { RoleName = RoleNames.Manager });
        Assert.Contains(managers.Items, u => u.EmployeeCode == "EMP002");
        Assert.DoesNotContain(managers.Items, u => u.EmployeeCode == "EMP001");

        var byBranchAndSearch = await repo.GetPagedAsync(new UserFilter { BranchId = 1, Search = "nhanvien.cg" });
        Assert.Equal("EMP003", Assert.Single(byBranchAndSearch.Items).EmployeeCode);

        Assert.True(await repo.ExistsByEmailAsync("admin@flexiwork.vn"));
        Assert.False(await repo.ExistsByEmailAsync("admin@flexiwork.vn", excludeId: admin.Id));
        Assert.True(await repo.ExistsByEmployeeCodeAsync("EMP002"));
    }

    [SkippableFact]
    public async Task SetRoles_replaces_all_roles_of_user()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var users = new UserRepository(session);
        var roles = new RoleRepository(session);
        var code = Unique("E");

        var userId = await users.AddAsync(new User
        {
            EmployeeCode = code,
            Email = $"{code}@test.local",
            PasswordHash = "x",
            FullName = "Người dùng kiểm thử",
            BranchId = 1
        });

        var employee = (await roles.GetByNameAsync(RoleNames.Employee))!;
        var hr = (await roles.GetByNameAsync(RoleNames.Hr))!;

        await users.SetRolesAsync(userId, new[] { employee.Id, hr.Id, hr.Id });
        Assert.Equal(new[] { RoleNames.Employee, RoleNames.Hr }, await users.GetRoleNamesAsync(userId));

        await users.SetRolesAsync(userId, new[] { employee.Id });
        Assert.Equal(new[] { RoleNames.Employee }, await users.GetRoleNamesAsync(userId));
        Assert.False(session.HasActiveTransaction);
    }

    [SkippableFact]
    public async Task Transaction_rolls_back_on_error_and_commits_on_success()
    {
        SkipIfUnavailable();
        var rolledBackCode = Unique("RB");
        var committedCode = Unique("CM");

        await using (var session = _db.CreateSession())
        {
            var repo = new DepartmentRepository(session);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.ExecuteInTransactionAsync(async ct =>
                {
                    await repo.AddAsync(new Department { Code = rolledBackCode, Name = "Sẽ bị hủy" }, ct);
                    throw new InvalidOperationException("Lỗi giả lập giữa nghiệp vụ");
                }));

            await session.ExecuteInTransactionAsync(ct =>
                repo.AddAsync(new Department { Code = committedCode, Name = "Được lưu" }, ct));

            Assert.False(session.HasActiveTransaction);
        }

        // Kiểm tra bằng phiên mới (kết nối khác) để chắc chắn dữ liệu đã/không được commit
        await using var verify = _db.CreateSession();
        var verifyRepo = new DepartmentRepository(verify);
        Assert.False(await verifyRepo.ExistsByCodeAsync(rolledBackCode));
        Assert.True(await verifyRepo.ExistsByCodeAsync(committedCode));
    }

    [SkippableFact]
    public async Task Branch_wifis_are_filtered_by_branch_and_active_flag()
    {
        SkipIfUnavailable();
        await using var session = _db.CreateSession();
        var branches = new BranchRepository(session);
        var wifis = new BranchWifiRepository(session);

        var branchId = await branches.AddAsync(new Branch
        {
            BranchCode = Unique("W-"), Name = "CN Wi-Fi", Address = "HN", Latitude = 21m, Longitude = 105m
        });

        await wifis.AddAsync(new BranchWifi { BranchId = branchId, Ssid = "A", Bssid = "AA:AA:AA:AA:AA:01" });
        await wifis.AddAsync(new BranchWifi { BranchId = branchId, Ssid = "B", Bssid = "AA:AA:AA:AA:AA:02", IsActive = false });

        Assert.Equal(2, (await wifis.GetByBranchIdAsync(branchId)).Count);
        Assert.Equal("A", Assert.Single(await wifis.GetByBranchIdAsync(branchId, activeOnly: true)).Ssid);
        Assert.True(await wifis.ExistsBssidAsync(branchId, "AA:AA:AA:AA:AA:01"));
        Assert.False(await wifis.ExistsBssidAsync(1, "AA:AA:AA:AA:AA:01"));
    }
}
