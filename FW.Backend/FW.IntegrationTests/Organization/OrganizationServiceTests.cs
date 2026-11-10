using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Features.Organization;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.Organization;

/// <summary>
/// Nghiệp vụ Chi nhánh / Wi-Fi / Phòng ban / Chức vụ trên MySQL thật, kèm kiểm tra phạm vi dữ liệu theo vai trò.
/// Dữ liệu seed: chi nhánh 1 (Cầu Giấy, có nhân sự) và 2 (Hoàn Kiếm); phòng ban 3 có nhân sự.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OrganizationServiceTests
{
    private readonly DatabaseFixture _db;

    public OrganizationServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int? branchId, params string[] roles)
        {
            BranchId = branchId;
            Roles = roles;
        }

        public int? UserId => 1;
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles { get; }
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private static readonly TestUser Admin = new(1, RoleNames.Admin);

    private static BranchService Branches(DbSession s, ICurrentUserService user) =>
        new(new BranchRepository(s), new BranchWifiRepository(s), new UserRepository(s), s, user);

    private static string Unique(string prefix, int length = 12) => $"{prefix}{Guid.NewGuid():N}"[..length];

    private static BranchRequest NewBranch(string code) => new()
    {
        BranchCode = code,
        Name = "Chi nhánh kiểm thử",
        Address = "Hà Nội",
        Latitude = 21.0285m,
        Longitude = 105.8542m,
        Radius = 80
    };

    [SkippableFact]
    public async Task Admin_manages_branch_and_wifi_with_normalized_values()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Branches(session, Admin);
        var code = Unique("cn-");

        var created = await service.CreateAsync(NewBranch($"  {code} "));
        Assert.Equal(code.ToUpperInvariant(), created.BranchCode);
        Assert.Equal(BranchStatus.Active, created.Status);

        // Trùng mã (không phân biệt hoa thường vì mã được chuẩn hóa)
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(NewBranch(code.ToLowerInvariant())));

        var wifi = await service.AddWifiAsync(created.Id, new BranchWifiRequest { Ssid = " Office ", Bssid = "aa-bb-cc-00-11-22" });
        Assert.Equal("AA:BB:CC:00:11:22", wifi.Bssid);
        Assert.Equal("Office", wifi.Ssid);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.AddWifiAsync(created.Id, new BranchWifiRequest { Ssid = "Trùng", Bssid = "AA:BB:CC:00:11:22" }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.AddWifiAsync(created.Id, new BranchWifiRequest { Ssid = "Sai", Bssid = "not-a-mac" }));

        var updated = await service.UpdateWifiAsync(created.Id, wifi.Id,
            new BranchWifiRequest { Ssid = "Office 5G", Bssid = "AA:BB:CC:00:11:22", IsActive = false });
        Assert.Equal("Office 5G", updated.Ssid);
        Assert.False(updated.IsActive);

        var detail = await service.GetByIdAsync(created.Id);
        Assert.Single(detail.Wifis);

        // Wi-Fi của chi nhánh khác không truy cập qua chi nhánh này được
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteWifiAsync(2, wifi.Id));

        await service.DeleteWifiAsync(created.Id, wifi.Id);
        Assert.Empty(await service.GetWifisAsync(created.Id));
    }

    [SkippableFact]
    public async Task Branch_update_changes_status_and_rejects_invalid_status()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Branches(session, Admin);
        var created = await service.CreateAsync(NewBranch(Unique("U-")));

        var request = NewBranch(created.BranchCode);
        request.Status = "INACTIVE";
        request.Radius = 200;
        var updated = await service.UpdateAsync(created.Id, request);
        Assert.Equal(BranchStatus.Inactive, updated.Status);
        Assert.Equal(200, updated.Radius);

        request.Status = "closed";
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(created.Id, request));

        var inactive = await service.GetPagedAsync(new BranchFilter { Status = BranchStatus.Inactive, Search = created.BranchCode });
        Assert.Equal(created.Id, Assert.Single(inactive.Items).Id);
    }

    [SkippableFact]
    public async Task Manager_only_sees_own_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Branches(session, new TestUser(1, RoleNames.Manager));

        // Dù cố tình lọc chi nhánh 2, Manager chi nhánh 1 vẫn chỉ nhận chi nhánh 1
        var page = await service.GetPagedAsync(new BranchFilter { BranchId = 2 });
        Assert.Equal(1, Assert.Single(page.Items).Id);

        Assert.NotNull(await service.GetByIdAsync(1));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(2));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetWifisAsync(2));
    }

    [SkippableFact]
    public async Task Hr_sees_all_branches_and_employee_sees_none()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();

        var hrPage = await Branches(session, new TestUser(null, RoleNames.Hr)).GetPagedAsync(new BranchFilter());
        Assert.True(hrPage.TotalCount >= 2);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Branches(session, new TestUser(1, RoleNames.Employee)).GetPagedAsync(new BranchFilter()));
    }

    [SkippableFact]
    public async Task Branch_with_staff_cannot_be_deleted_but_empty_branch_can()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = Branches(session, Admin);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(1));
        Assert.Contains("nhân sự", ex.Message);

        var created = await service.CreateAsync(NewBranch(Unique("D-")));
        await service.AddWifiAsync(created.Id, new BranchWifiRequest { Ssid = "X", Bssid = "11:22:33:44:55:66" });

        await service.DeleteAsync(created.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(created.Id));
        Assert.Empty(await new BranchWifiRepository(session).GetByBranchIdAsync(created.Id)); // Wi-Fi bị xóa theo
    }

    [SkippableFact]
    public async Task Department_crud_with_unique_code_and_staff_guard()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = new DepartmentService(new DepartmentRepository(session), new UserRepository(session));
        var code = Unique("dep-");

        var created = await service.CreateAsync(new DepartmentRequest { Code = code, Name = " Phòng Marketing ", Description = "  " });
        Assert.Equal(code.ToUpperInvariant(), created.Code);
        Assert.Equal("Phòng Marketing", created.Name);
        Assert.Null(created.Description);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateAsync(new DepartmentRequest { Code = code, Name = "Trùng mã" }));

        var updated = await service.UpdateAsync(created.Id, new DepartmentRequest { Code = code, Name = "Marketing", Description = "Truyền thông" });
        Assert.Equal("Truyền thông", updated.Description);

        await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(3)); // phòng ban 3 có nhân sự trong seed
        await service.DeleteAsync(created.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(created.Id));

        var page = await service.GetPagedAsync(new PagedQuery { Search = "Vận hành" });
        Assert.Contains(page.Items, d => d.Code == "DEP-OPS");
    }

    [SkippableFact]
    public async Task Position_rejects_duplicate_title_and_negative_salary()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var service = new PositionService(new PositionRepository(session), new UserRepository(session));
        var title = Unique("Chức danh ", 20);

        var created = await service.CreateAsync(new PositionRequest { Title = title, BaseSalaryPerHour = 45000.456m });
        Assert.Equal(45000.46m, created.BaseSalaryPerHour);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateAsync(new PositionRequest { Title = title.ToUpperInvariant(), BaseSalaryPerHour = 1 }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(created.Id, new PositionRequest { Title = title, BaseSalaryPerHour = -1 }));

        // Đổi tên trùng chức danh đang có trong seed
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(created.Id, new PositionRequest { Title = "Tổng Giám Đốc", BaseSalaryPerHour = 1 }));

        await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(4)); // chức danh 4 có nhân sự
        await service.DeleteAsync(created.Id);
    }
}
