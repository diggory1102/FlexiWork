using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Payroll;
using FW.Application.Features.Scheduling;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Payroll;

/// <summary>
/// Cấu hình lương (Task 5.4) trên MySQL thật: hệ số OT, danh mục phụ cấp, lương cơ bản / đơn giá giờ theo nhân viên và phụ cấp gán riêng.
/// Dữ liệu seed: phụ cấp 1 = ca đêm, 2 = ca gãy (áp dụng cho tất cả), 3 = ăn trưa (gán riêng); chức vụ 4 có đơn giá giờ 35.000đ.
/// Người dùng 4 = HR, 1 = Admin.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PayrollConfigServiceTests
{
    private const int HrId = 4, StaffPosition = 4;

    private readonly DatabaseFixture _db;

    public PayrollConfigServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class TestUser : ICurrentUserService
    {
        private readonly string[] _roles;

        public TestUser(int userId, params string[] roles)
        {
            UserId = userId;
            _roles = roles;
        }

        public int? UserId { get; }
        public int? BranchId => null;
        public IReadOnlyList<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => _roles.Contains(role);
    }

    private static PayrollConfigService Service(DbSession s, int userId = HrId) => new(
        new AllowanceRepository(s), new UserAllowanceRepository(s), new PayrollConfigRepository(s), new SystemSettingRepository(s),
        new AttendancePolicyService(new ViolationRuleRepository(s), new SystemSettingRepository(s), s), s, new TestUser(userId, RoleNames.Hr),
        NullLogger<PayrollConfigService>.Instance);

    private static async Task<User> NewEmployeeAsync(DbSession s, string type = EmploymentType.FullTime, int? positionId = StaffPosition)
    {
        var code = $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Nhân viên {code}",
            BranchId = 1, PositionId = positionId, EmploymentType = type
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static AllowanceRequest Allowance(string? name = null, string basis = AllowanceBasis.Monthly, decimal amount = 100000, bool all = false) => new()
    {
        Name = name ?? $"Phụ cấp {Guid.NewGuid():N}"[..24], Basis = basis, Amount = amount, ApplyToAll = all
    };

    private static async Task<EmployeePayDto> FindAsync(PayrollConfigService service, User user) =>
        Assert.Single((await service.GetEmployeesAsync(new PayrollEmployeeFilter { Search = user.EmployeeCode })).Items);

    // ------------------------------------------------------------------ Tham số chung

    [SkippableFact]
    public async Task Overtime_multiplier_defaults_to_1_5_and_is_validated()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);

        var settings = await service.GetSettingsAsync();
        Assert.Equal(1.5m, settings.OvertimeMultiplier);
        Assert.Equal(8m, settings.StandardHoursPerDay);
        Assert.True(settings.StandardWorkDays > 0);

        foreach (var invalid in new[] { 0.5m, 0m, 5.5m })
        {
            await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateSettingsAsync(new UpdatePayrollSettingsRequest { OvertimeMultiplier = invalid }));
        }
        Assert.Equal(2m, (await service.UpdateSettingsAsync(new UpdatePayrollSettingsRequest { OvertimeMultiplier = 2.004m })).OvertimeMultiplier);

        await service.UpdateSettingsAsync(new UpdatePayrollSettingsRequest { OvertimeMultiplier = 1.5m }); // trả về mặc định
        Assert.Equal(1.5m, (await service.GetSettingsAsync()).OvertimeMultiplier);
    }

    // ------------------------------------------------------------------ Phụ cấp

    [SkippableFact]
    public async Task Allowance_catalog_is_validated_and_protected_once_assigned()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);

        var seeded = await service.GetAllowancesAsync(activeOnly: false);
        Assert.Contains(seeded, a => a.Name == "Phụ cấp ca đêm" && a is { Basis: AllowanceBasis.NightShift, ApplyToAll: true });

        var created = await service.CreateAllowanceAsync(Allowance(basis: AllowanceBasis.SplitDay, amount: 25000));
        Assert.Equal((AllowanceBasis.SplitDay, 25000m, true, 0), (created.Basis, created.Amount, created.IsActive, created.AssignedCount));
        Assert.Equal("mỗi ngày ca gãy", created.BasisLabel);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAllowanceAsync(Allowance(created.Name)));                       // trùng tên
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAllowanceAsync(Allowance(basis: "weekly")));                     // cách tính lạ
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAllowanceAsync(Allowance(amount: 0)));                           // phải > 0
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAllowanceAsync(Allowance(amount: 1000.5m)));                     // đồng nguyên
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAllowanceAsync(Allowance(amount: 100_000_001)));                 // quá giới hạn
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAllowanceAsync(Allowance(name: "   ")));

        var updated = await service.UpdateAllowanceAsync(created.Id, Allowance(created.Name, AllowanceBasis.SplitDay, 30000, all: true));
        Assert.Equal((30000m, true), (updated.Amount, updated.ApplyToAll));
        Assert.DoesNotContain(await service.GetAllowancesAsync(activeOnly: true), a => a.Id == created.Id && !a.IsActive);

        // Đã gán cho nhân viên: không đổi cách tính, không xóa
        var employee = await NewEmployeeAsync(s);
        await service.SetEmployeeAllowancesAsync(employee.Id, new SetEmployeeAllowancesRequest
        {
            Items = { new EmployeeAllowanceInput { AllowanceId = created.Id, Amount = 40000 } }
        });
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAllowanceAsync(created.Id, Allowance(created.Name, AllowanceBasis.NightShift, 30000)));
        var inUse = await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAllowanceAsync(created.Id));
        Assert.Contains("1 nhân viên", inUse.Message);

        await service.SetEmployeeAllowancesAsync(employee.Id, new SetEmployeeAllowancesRequest());
        await service.DeleteAllowanceAsync(created.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAllowanceAsync(created.Id, Allowance()));
    }

    // ------------------------------------------------------------------ Lương theo nhân viên

    [SkippableFact]
    public async Task Full_time_salary_makes_the_employee_configured_and_sets_the_overtime_rate()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);
        var employee = await NewEmployeeAsync(s);

        var before = await FindAsync(service, employee);
        Assert.False(before.IsConfigured);
        Assert.Null(before.BaseSalary);
        Assert.Equal(35000m, before.EffectiveHourlyRate); // theo chức vụ
        Assert.Equal(35000m, before.OvertimeHourlyRate);  // chưa có lương tháng → dùng đơn giá giờ

        var detail = await service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest { BaseSalary = 10_400_000 });
        var standardDays = (await service.GetSettingsAsync()).StandardWorkDays;
        Assert.True(detail.Pay.IsConfigured);
        Assert.Equal(10_400_000m, detail.Pay.BaseSalary);
        Assert.Equal(Math.Round(10_400_000m / standardDays / 8m, 2), detail.Pay.OvertimeHourlyRate);

        // Lọc theo trạng thái cấu hình và loại hợp đồng
        var search = new PayrollEmployeeFilter { Search = employee.EmployeeCode, Configured = false };
        Assert.Empty((await service.GetEmployeesAsync(search)).Items);
        search.Configured = true;
        Assert.Single((await service.GetEmployeesAsync(search)).Items);
        search.EmploymentType = EmploymentType.PartTime;
        Assert.Empty((await service.GetEmployeesAsync(search)).Items);
        await Assert.ThrowsAsync<BadRequestException>(() => service.GetEmployeesAsync(new PayrollEmployeeFilter { EmploymentType = "contract" }));

        // Bỏ trống = xóa cấu hình
        Assert.False((await service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest())).Pay.IsConfigured);
    }

    [SkippableFact]
    public async Task Part_time_pay_uses_the_position_rate_unless_overridden()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);
        var withPosition = await NewEmployeeAsync(s, EmploymentType.PartTime);
        var noPosition = await NewEmployeeAsync(s, EmploymentType.PartTime, positionId: null);

        var pay = await FindAsync(service, withPosition);
        Assert.Equal((true, 35000m, null), (pay.IsConfigured, pay.EffectiveHourlyRate, pay.HourlyRate));
        Assert.False((await FindAsync(service, noPosition)).IsConfigured); // không chức vụ, không mức riêng

        var overridden = await service.UpdateEmployeePayAsync(withPosition.Id, new UpdateEmployeePayRequest { HourlyRate = 45000.555m });
        Assert.Equal((45000.56m, 45000.56m, 35000m), (overridden.Pay.HourlyRate, overridden.Pay.EffectiveHourlyRate, overridden.Pay.PositionHourlyRate));
        Assert.Equal(overridden.Pay.EffectiveHourlyRate, overridden.Pay.OvertimeHourlyRate);

        var fixedNoPosition = await service.UpdateEmployeePayAsync(noPosition.Id, new UpdateEmployeePayRequest { HourlyRate = 30000 });
        Assert.True(fixedNoPosition.Pay.IsConfigured);
    }

    [SkippableFact]
    public async Task Pay_updates_are_validated_and_not_allowed_on_yourself()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);
        var employee = await NewEmployeeAsync(s);

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest { BaseSalary = 9_000_000.5m }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest { BaseSalary = -1 }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest { BaseSalary = 1_000_000_001 }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest { HourlyRate = 0 }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateEmployeePayAsync(employee.Id, new UpdateEmployeePayRequest { HourlyRate = 10_000_001 }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateEmployeePayAsync(99_999_999, new UpdateEmployeePayRequest { BaseSalary = 1000 }));
        Assert.Null((await service.GetEmployeeAsync(employee.Id)).Pay.BaseSalary); // lỗi thì không ghi gì

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateEmployeePayAsync(HrId, new UpdateEmployeePayRequest { BaseSalary = 99_000_000 }));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SetEmployeeAllowancesAsync(HrId, new SetEmployeeAllowancesRequest()));
    }

    // ------------------------------------------------------------------ Phụ cấp của nhân viên

    [SkippableFact]
    public async Task Employee_allowances_combine_apply_to_all_assignments_and_overrides()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);
        var employee = await NewEmployeeAsync(s);
        var catalog = await service.GetAllowancesAsync(activeOnly: false);
        var night = catalog.First(a => a.Name == "Phụ cấp ca đêm");
        var lunch = catalog.First(a => a.Name == "Phụ cấp ăn trưa");
        var everyone = catalog.Count(a => a.ApplyToAll && a.IsActive);

        // Chưa gán gì: chỉ hưởng phụ cấp áp dụng cho tất cả
        var start = await service.GetEmployeeAsync(employee.Id);
        Assert.Equal(everyone, start.Pay.AllowanceCount);
        Assert.True(start.Allowances.Single(a => a.AllowanceId == night.Id).Assigned);
        Assert.False(start.Allowances.Single(a => a.AllowanceId == lunch.Id).Assigned);

        // Gán ăn trưa (mức danh mục) và đặt mức ca đêm riêng
        var set = await service.SetEmployeeAllowancesAsync(employee.Id, new SetEmployeeAllowancesRequest
        {
            Items =
            {
                new EmployeeAllowanceInput { AllowanceId = lunch.Id },
                new EmployeeAllowanceInput { AllowanceId = night.Id, Amount = 45000 }
            }
        });
        Assert.Equal(everyone + 1, set.Pay.AllowanceCount);
        var lunchRow = set.Allowances.Single(a => a.AllowanceId == lunch.Id);
        Assert.Equal((true, null, lunch.Amount), (lunchRow.Assigned, lunchRow.OverrideAmount, lunchRow.EffectiveAmount));
        var nightRow = set.Allowances.Single(a => a.AllowanceId == night.Id);
        Assert.Equal((45000m, 45000m, night.Amount), (nightRow.OverrideAmount, nightRow.EffectiveAmount, nightRow.DefaultAmount));

        // Gán lại = thay toàn bộ: bỏ ăn trưa, mức ca đêm về danh mục
        var replaced = await service.SetEmployeeAllowancesAsync(employee.Id, new SetEmployeeAllowancesRequest());
        Assert.Equal(everyone, replaced.Pay.AllowanceCount);
        Assert.False(replaced.Allowances.Single(a => a.AllowanceId == lunch.Id).Assigned);
        Assert.Equal(night.Amount, replaced.Allowances.Single(a => a.AllowanceId == night.Id).EffectiveAmount);
    }

    [SkippableFact]
    public async Task Allowance_assignments_are_validated_and_inactive_ones_stop_counting()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var service = Service(s);
        var employee = await NewEmployeeAsync(s);
        var catalog = await service.GetAllowancesAsync(activeOnly: false);
        var night = catalog.First(a => a.Name == "Phụ cấp ca đêm");
        var lunch = catalog.First(a => a.Name == "Phụ cấp ăn trưa");

        SetEmployeeAllowancesRequest Items(params EmployeeAllowanceInput[] items) => new() { Items = items.ToList() };
        await Assert.ThrowsAsync<BadRequestException>(() => service.SetEmployeeAllowancesAsync(employee.Id, Items(
            new EmployeeAllowanceInput { AllowanceId = lunch.Id }, new EmployeeAllowanceInput { AllowanceId = lunch.Id })));          // gán hai lần
        await Assert.ThrowsAsync<BadRequestException>(() => service.SetEmployeeAllowancesAsync(employee.Id, Items(
            new EmployeeAllowanceInput { AllowanceId = night.Id })));                                                                   // áp dụng cho tất cả: phải có mức riêng
        await Assert.ThrowsAsync<BadRequestException>(() => service.SetEmployeeAllowancesAsync(employee.Id, Items(
            new EmployeeAllowanceInput { AllowanceId = 99_999_999 })));
        await Assert.ThrowsAsync<BadRequestException>(() => service.SetEmployeeAllowancesAsync(employee.Id, Items(
            new EmployeeAllowanceInput { AllowanceId = lunch.Id, Amount = 0 })));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetEmployeeAllowancesAsync(99_999_999, Items()));
        Assert.Equal(0, (await service.GetEmployeeAsync(employee.Id)).Allowances.Count(a => a.OverrideAmount.HasValue)); // lỗi thì không ghi gì

        // Phụ cấp ngừng áp dụng không còn được tính vào số phụ cấp đang hưởng
        var extra = await service.CreateAllowanceAsync(Allowance());
        await service.SetEmployeeAllowancesAsync(employee.Id, Items(new EmployeeAllowanceInput { AllowanceId = extra.Id }));
        var withExtra = (await service.GetEmployeeAsync(employee.Id)).Pay.AllowanceCount;
        var request = Allowance(extra.Name, extra.Basis, extra.Amount);
        request.IsActive = false;
        await service.UpdateAllowanceAsync(extra.Id, request);
        Assert.Equal(withExtra - 1, (await service.GetEmployeeAsync(employee.Id)).Pay.AllowanceCount);

        await service.SetEmployeeAllowancesAsync(employee.Id, Items());
        await service.DeleteAllowanceAsync(extra.Id);
    }
}
