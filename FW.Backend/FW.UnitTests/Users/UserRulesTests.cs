using FW.Application.Common.Exceptions;
using FW.Application.Common.Security;
using FW.Application.Features.Users;
using FW.Domain.Enums;
using FW.UnitTests.Fakes;

namespace FW.UnitTests.Users;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generated_passwords_satisfy_policy_and_are_unique()
    {
        var passwords = Enumerable.Range(0, 200).Select(_ => PasswordGenerator.Generate()).ToList();

        Assert.All(passwords, p =>
        {
            Assert.Equal(12, p.Length);
            Assert.Empty(PasswordPolicy.Validate(p));
            Assert.DoesNotContain(p, c => "0O1lI".Contains(c)); // không có ký tự dễ nhầm
        });
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }

    [Fact]
    public void Length_below_policy_minimum_is_raised() =>
        Assert.Equal(PasswordPolicy.MinLength, PasswordGenerator.Generate(4).Length);
}

public class UserManagementRulesTests
{
    private static FakeCurrentUser User(int id, params string[] roles) => new(1, roles) { UserId = id };

    [Fact]
    public void Admin_can_assign_every_role_and_manage_everyone()
    {
        var admin = User(1, RoleNames.Admin);

        Assert.Equal(RoleNames.All, UserManagementRules.AssignableRoles(admin));
        UserManagementRules.EnsureCanAssign(admin, new[] { RoleNames.Admin, RoleNames.Hr });
        UserManagementRules.EnsureCanManage(admin, 2, new[] { RoleNames.Admin });
    }

    [Fact]
    public void Hr_assigns_only_employee_or_manager_and_cannot_manage_admin()
    {
        var hr = User(4, RoleNames.Hr);

        UserManagementRules.EnsureCanAssign(hr, new[] { RoleNames.Employee, RoleNames.Manager });
        Assert.Throws<ForbiddenException>(() => UserManagementRules.EnsureCanAssign(hr, new[] { RoleNames.Admin }));
        Assert.Throws<ForbiddenException>(() => UserManagementRules.EnsureCanAssign(hr, new[] { RoleNames.Hr }));

        UserManagementRules.EnsureCanManage(hr, 3, new[] { RoleNames.Employee });
        Assert.Throws<ForbiddenException>(() => UserManagementRules.EnsureCanManage(hr, 1, new[] { RoleNames.Admin }));
    }

    [Fact]
    public void Manager_cannot_assign_or_manage_accounts()
    {
        var manager = User(2, RoleNames.Manager);

        Assert.Empty(UserManagementRules.AssignableRoles(manager));
        Assert.Throws<ForbiddenException>(() => UserManagementRules.EnsureCanManage(manager, 3, new[] { RoleNames.Employee }));
    }

    [Fact]
    public void Nobody_can_act_on_own_account()
    {
        Assert.Throws<BadRequestException>(() => UserManagementRules.EnsureNotSelf(User(1, RoleNames.Admin), 1, "khóa"));
        UserManagementRules.EnsureNotSelf(User(1, RoleNames.Admin), 2, "khóa");
    }
}
