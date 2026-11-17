using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Features.Notifications;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using FW.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Notifications;

/// <summary>
/// Thông báo (Task 5.1) trên MySQL thật: gửi cho nhiều người (lưu + đẩy tức thì, không bao giờ làm hỏng nghiệp vụ), chọn người duyệt
/// theo chi nhánh, danh sách / chưa đọc / đánh dấu đã đọc chỉ trong phạm vi của chính mình. Seed: người dùng 2 = Quản lý chi nhánh 1,
/// 4 = HR.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NotificationServiceTests
{
    private const int Branch1 = 1, ManagerId = 2, HrId = 4;

    private readonly DatabaseFixture _db;

    public NotificationServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    private sealed class RecordingPublisher : INotificationPublisher
    {
        public List<(IReadOnlyCollection<int> Users, RealtimeNotification Notification)> Calls { get; } = new();
        public bool Throw { get; set; }

        public Task PublishAsync(IReadOnlyCollection<int> userIds, RealtimeNotification notification, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("Hub không khả dụng");
            Calls.Add((userIds, notification));
            return Task.CompletedTask;
        }
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(int userId) => UserId = userId;
        public int? UserId { get; }
        public int? BranchId => Branch1;
        public IReadOnlyList<string> Roles => new[] { RoleNames.Employee };
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == RoleNames.Employee;
    }

    private static Notifier NewNotifier(DbSession s, RecordingPublisher publisher) =>
        new(new NotificationRepository(s), new UserRepository(s), publisher, new DateTimeProvider(), NullLogger<Notifier>.Instance);

    private static async Task<User> NewUserAsync(DbSession s, int? branchId, params string[] roles)
    {
        var code = $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x",
            FullName = $"Người nhận {code}", BranchId = branchId
        };
        await new UserRepository(s).AddAsync(user);
        if (roles.Length > 0)
        {
            var ids = (await new RoleRepository(s).GetByNamesAsync(roles)).Select(r => r.Id);
            await new UserRepository(s).SetRolesAsync(user.Id, ids);
        }
        return user;
    }

    private static Task<int> UnreadAsync(DbSession s, int userId) => new NotificationRepository(s).CountUnreadAsync(userId);

    [SkippableFact]
    public async Task Notify_saves_for_each_recipient_pushes_once_and_never_throws()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewUserAsync(s, Branch1);
        var b = await NewUserAsync(s, Branch1);
        var publisher = new RecordingPublisher();
        var notifier = NewNotifier(s, publisher);

        await notifier.NotifyAsync(new[] { a.Id, b.Id, a.Id }, NotificationType.LeaveApproval, new string('T', 200), "Nội dung", "42");

        // Người trùng chỉ nhận một lần; tiêu đề quá dài bị cắt; chưa đọc
        foreach (var user in new[] { a, b })
        {
            var page = await new NotificationRepository(s).GetPagedForUserAsync(user.Id, new NotificationQuery());
            var saved = Assert.Single(page.Items);
            Assert.Equal((user.Id, NotificationType.LeaveApproval, "Nội dung", "42", false), (saved.UserId, saved.Type, saved.Content, saved.ReferenceId, saved.IsRead));
            Assert.Equal(Notifier.MaxTitleLength, saved.Title.Length);
            Assert.EndsWith("…", saved.Title);
        }
        var (pushed, payload) = Assert.Single(publisher.Calls);
        Assert.Equal(new[] { a.Id, b.Id }, pushed.OrderBy(x => x));
        Assert.Equal((NotificationType.LeaveApproval, "42", "Nội dung"), (payload.Type, payload.ReferenceId, payload.Content));

        // Hub lỗi hoặc người nhận không tồn tại: không ném lỗi — thông báo hỏng không được làm hỏng nghiệp vụ chính
        publisher.Throw = true;
        await notifier.NotifyAsync(new[] { a.Id }, NotificationType.Attendance, "Vẫn lưu", "x");
        Assert.Equal(2, await UnreadAsync(s, a.Id));
        await notifier.NotifyAsync(new[] { -12345 }, NotificationType.Attendance, "Không có người này", "x");
        await notifier.NotifyAsync(Array.Empty<int>(), NotificationType.Attendance, "Không ai", "x");
        Assert.Single(publisher.Calls);
    }

    [SkippableFact]
    public async Task Reviewers_are_the_branch_managers_then_HR()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var branch = new Branch
        {
            BranchCode = $"N{Guid.NewGuid():N}"[..10].ToUpperInvariant(), Name = "Chi nhánh thử thông báo", Address = "1 Phố Thử",
            Latitude = 21.0m, Longitude = 105.8m, Radius = 50
        };
        var branchId = await new BranchRepository(s).AddAsync(branch);
        var employee = await NewUserAsync(s, branchId);
        var publisher = new RecordingPublisher();
        var notifier = NewNotifier(s, publisher);

        async Task<(int Hr, int Seed)> SnapshotAsync() => (await UnreadAsync(s, HrId), await UnreadAsync(s, ManagerId));

        // Chi nhánh chưa có Quản lý → gửi HR
        var before = await SnapshotAsync();
        await notifier.NotifyReviewersAsync(branchId, employee.Id, NotificationType.LeaveRequest, "Đơn mới", "x");
        Assert.Equal(before.Hr + 1, await UnreadAsync(s, HrId));

        // Có Quản lý → chỉ Quản lý chi nhánh đó nhận, HR không
        var manager = await NewUserAsync(s, branchId, RoleNames.Manager);
        before = await SnapshotAsync();
        await notifier.NotifyReviewersAsync(branchId, employee.Id, NotificationType.LeaveRequest, "Đơn mới", "x");
        Assert.Equal(1, await UnreadAsync(s, manager.Id));
        Assert.Equal(before, await SnapshotAsync()); // Quản lý chi nhánh khác và HR không nhận thêm

        // Người tạo là Quản lý → HR duyệt, Quản lý không tự nhận thông báo của mình
        before = await SnapshotAsync();
        await notifier.NotifyReviewersAsync(branchId, manager.Id, NotificationType.LeaveRequest, "Đơn của Quản lý", "x");
        Assert.Equal(before.Hr + 1, await UnreadAsync(s, HrId));
        Assert.Equal(1, await UnreadAsync(s, manager.Id));

        // Không rõ chi nhánh → HR
        before = await SnapshotAsync();
        await notifier.NotifyReviewersAsync(null, employee.Id, NotificationType.LeaveRequest, "Đơn không chi nhánh", "x");
        Assert.Equal(before.Hr + 1, await UnreadAsync(s, HrId));
    }

    [SkippableFact]
    public async Task Users_list_count_and_read_only_their_own_notifications()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var me = await NewUserAsync(s, Branch1);
        var other = await NewUserAsync(s, Branch1);
        var notifier = NewNotifier(s, new RecordingPublisher());
        foreach (var i in new[] { 1, 2, 3 })
        {
            await notifier.NotifyAsync(new[] { me.Id }, NotificationType.ScheduleNew, $"Thông báo {i}", "Nội dung", $"ref-{i}");
        }
        await notifier.NotifyAsync(new[] { other.Id }, NotificationType.ScheduleNew, "Của người khác", "x");

        var mine = new NotificationService(new NotificationRepository(s), new TestUser(me.Id));
        var theirs = new NotificationService(new NotificationRepository(s), new TestUser(other.Id));

        // Mới nhất trước, phân trang; không lẫn thông báo của người khác
        var first = await mine.GetMineAsync(new NotificationFilter { PageSize = 2 });
        Assert.Equal((3, 2), (first.TotalCount, first.TotalPages));
        Assert.Equal(new[] { "Thông báo 3", "Thông báo 2" }, first.Items.Select(n => n.Title));
        Assert.Equal("ref-3", first.Items[0].ReferenceId);
        Assert.Equal(3, (await mine.GetUnreadCountAsync()).Count);

        // Đánh dấu đã đọc: gọi lại không lỗi; người khác không đánh dấu hộ được
        var id = first.Items[0].Id;
        await mine.MarkReadAsync(id);
        await mine.MarkReadAsync(id);
        Assert.Equal(2, (await mine.GetUnreadCountAsync()).Count);
        Assert.Equal(2, (await mine.GetMineAsync(new NotificationFilter { UnreadOnly = true })).TotalCount);
        Assert.True((await mine.GetMineAsync(new NotificationFilter())).Items.Single(n => n.Id == id).IsRead);
        await Assert.ThrowsAsync<NotFoundException>(() => theirs.MarkReadAsync(first.Items[1].Id));
        await Assert.ThrowsAsync<NotFoundException>(() => mine.MarkReadAsync(-1));
        Assert.Equal(1, (await theirs.GetUnreadCountAsync()).Count);

        // Đánh dấu tất cả
        Assert.Equal(2, await mine.MarkAllReadAsync());
        Assert.Equal(0, (await mine.GetUnreadCountAsync()).Count);
        Assert.Equal(0, await mine.MarkAllReadAsync());
        Assert.Equal(1, (await theirs.GetUnreadCountAsync()).Count);
    }
}
