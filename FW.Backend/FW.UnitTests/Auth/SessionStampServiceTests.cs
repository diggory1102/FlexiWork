using FW.Application.Features.Auth;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.UnitTests.Fakes;

namespace FW.UnitTests.Auth;

/// <summary>Thu hồi phiên khi tài khoản thay đổi: khóa, cho nghỉ, xóa, đổi vai trò, đổi chi nhánh.</summary>
public class SessionStampServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeUserRepository _users = new();
    private readonly LiveSessionRegistry _liveSessions = new();
    private readonly SessionStampService _sessions;
    private readonly User _employee;

    public SessionStampServiceTests()
    {
        _sessions = new SessionStampService(_users, new FakeCache(_clock), _clock, _liveSessions);
        _employee = AddUser("nv@flexiwork.vn", RoleNames.Employee);
    }

    private User AddUser(string email, params string[] roles) => _users.Add(new User
    {
        EmployeeCode = "EMP" + (_users.Users.Count + 1),
        Email = email,
        FullName = "Nhân viên",
        BranchId = 1,
        Status = UserStatus.Active
    }, roles);

    private string StampOf(User user) => SessionStampService.StampOf(user, _users.Roles[user.Id]);

    /// <summary>Cấp một phiên hợp lệ cho nhân viên và nạp dấu phiên vào cache như khi đang sử dụng.</summary>
    private async Task<string> SignedIn()
    {
        var stamp = StampOf(_employee);
        Assert.True(await _sessions.IsCurrentAsync(_employee.Id, stamp));
        return stamp;
    }

    private async Task AssertRevoked(string stamp)
    {
        await _sessions.InvalidateAsync(_employee.Id);
        Assert.False(await _sessions.IsCurrentAsync(_employee.Id, stamp));
    }

    [Theory]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.Left)]
    public async Task Inactive_account_loses_its_sessions_and_gets_none(string status)
    {
        var stamp = await SignedIn();
        _employee.Status = status;

        await AssertRevoked(stamp);
        Assert.False(await _sessions.IsCurrentAsync(_employee.Id, StampOf(_employee))); // kể cả dấu phiên tính từ trạng thái mới
    }

    [Fact]
    public async Task Deleted_or_unknown_account_has_no_session()
    {
        var stamp = await SignedIn();
        _employee.IsDeleted = true;

        await AssertRevoked(stamp);
        Assert.False(await _sessions.IsCurrentAsync(999, stamp));
    }

    [Fact]
    public async Task Role_change_revokes_old_session_and_new_login_is_accepted()
    {
        var stamp = await SignedIn();
        _users.Roles[_employee.Id] = new() { RoleNames.Manager };

        await AssertRevoked(stamp);
        Assert.True(await _sessions.IsCurrentAsync(_employee.Id, StampOf(_employee)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(null)]
    public async Task Branch_change_revokes_old_session(int? newBranchId)
    {
        var stamp = await SignedIn();
        _employee.BranchId = newBranchId;

        await AssertRevoked(stamp);
    }

    [Fact]
    public async Task Changes_that_do_not_affect_access_keep_the_session()
    {
        var stamp = await SignedIn();
        _employee.FullName = "Tên mới";
        _employee.PhoneNumber = "0900000000";

        await _sessions.InvalidateAsync(_employee.Id);

        Assert.True(await _sessions.IsCurrentAsync(_employee.Id, stamp));
    }

    [Fact]
    public void Stamp_ignores_role_order_and_distinguishes_role_sets()
    {
        var both = SessionStampService.StampOf(_employee, new[] { RoleNames.Manager, RoleNames.Employee });

        Assert.Equal(both, SessionStampService.StampOf(_employee, new[] { RoleNames.Employee, RoleNames.Manager }));
        Assert.NotEqual(both, SessionStampService.StampOf(_employee, new[] { RoleNames.Manager }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public async Task Token_without_a_matching_stamp_is_rejected(string? stamp)
    {
        Assert.False(await _sessions.IsCurrentAsync(_employee.Id, stamp));
    }

    [Fact]
    public async Task Change_made_directly_in_database_takes_effect_when_cache_expires()
    {
        var stamp = await SignedIn();
        _employee.Status = UserStatus.Suspended; // không qua ứng dụng nên không ai gọi InvalidateAsync

        _clock.Advance(SessionStampService.CacheLifetime - TimeSpan.FromSeconds(1));
        Assert.True(await _sessions.IsCurrentAsync(_employee.Id, stamp));

        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(await _sessions.IsCurrentAsync(_employee.Id, stamp));
    }

    [Fact]
    public async Task Revoking_a_session_disconnects_only_that_users_connections()
    {
        var colleague = AddUser("khac@flexiwork.vn", RoleNames.Employee);
        var aborted = new List<string>();
        var stamp = await SignedIn();
        _liveSessions.Add(_employee.Id, "web", stamp, () => aborted.Add("web"));
        _liveSessions.Add(_employee.Id, "mobile", stamp, () => aborted.Add("mobile"));
        _liveSessions.Add(colleague.Id, "other", StampOf(colleague), () => aborted.Add("other"));

        _employee.FullName = "Tên mới";
        await _sessions.InvalidateAsync(_employee.Id);
        Assert.Empty(aborted);

        _employee.Status = UserStatus.Suspended;
        await _sessions.InvalidateAsync(_employee.Id);

        Assert.Equal(new[] { "mobile", "web" }, aborted.Order());
        Assert.True(_liveSessions.HasConnections(colleague.Id));
    }
}
