using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Features.Chat;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.IntegrationTests.Chat;

/// <summary>
/// Chat nội bộ (Task 5.2) trên MySQL thật: chat 1-1 (mở trùng phòng, gửi, lịch sử phân trang, chưa đọc), ảnh và thu hồi tin, nhóm chi nhánh
/// tự đồng bộ thành viên, quyền theo thành viên / chi nhánh, danh bạ. Mỗi test tạo người dùng và chi nhánh riêng để không lẫn nhau.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ChatServiceTests
{
    private readonly DatabaseFixture _db;

    public ChatServiceTests(DatabaseFixture db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ test doubles

    private sealed class RecordingPublisher : IChatPublisher
    {
        public List<(IReadOnlyCollection<int> Users, ChatMessageDto Message)> Created { get; } = new();
        public List<(IReadOnlyCollection<int> Users, int ConversationId, long MessageId)> Deleted { get; } = new();
        public List<(int UserId, int ConversationId, int Unread)> Read { get; } = new();
        public bool Throw { get; set; }

        public Task MessageCreatedAsync(IReadOnlyCollection<int> userIds, ChatMessageDto message, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("Hub không khả dụng");
            Created.Add((userIds, message));
            return Task.CompletedTask;
        }

        public Task MessageDeletedAsync(IReadOnlyCollection<int> userIds, int conversationId, long messageId, CancellationToken cancellationToken = default)
        {
            Deleted.Add((userIds, conversationId, messageId));
            return Task.CompletedTask;
        }

        public Task ConversationReadAsync(int userId, int conversationId, int unreadCount, CancellationToken cancellationToken = default)
        {
            Read.Add((userId, conversationId, unreadCount));
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        public List<string> Uploaded { get; } = new();
        public List<string> Deleted { get; } = new();

        public Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            var id = $"{folder}/{Guid.NewGuid():N}";
            Uploaded.Add(id);
            return Task.FromResult(new FileUploadResult($"https://img.test/{id}.jpg", id));
        }

        public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(publicId);
            return Task.FromResult(true);
        }
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(User user)
        {
            UserId = user.Id;
            BranchId = user.BranchId;
        }

        public int? UserId { get; }
        public int? BranchId { get; }
        public IReadOnlyList<string> Roles => new[] { RoleNames.Employee };
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => role == RoleNames.Employee;
    }

    private static ChatService As(DbSession s, User user, RecordingPublisher? publisher = null, MemoryStorage? storage = null) => new(
        new ChatRepository(s), new UserRepository(s), new BranchRepository(s), new PositionRepository(s), publisher ?? new RecordingPublisher(),
        storage ?? new MemoryStorage(), new TestUser(user), NullLogger<ChatService>.Instance);

    // ------------------------------------------------------------------ dữ liệu

    private static async Task<int> NewBranchAsync(DbSession s) =>
        await new BranchRepository(s).AddAsync(new Branch
        {
            BranchCode = $"C{Guid.NewGuid():N}"[..10].ToUpperInvariant(), Name = $"Chi nhánh chat {Guid.NewGuid():N}"[..24], Address = "1 Phố Thử",
            Latitude = 21.0m, Longitude = 105.8m, Radius = 50
        });

    private static async Task<User> NewUserAsync(DbSession s, int? branchId = null, string status = UserStatus.Active)
    {
        var code = $"H{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Chat {code}",
            BranchId = branchId, Status = status
        };
        await new UserRepository(s).AddAsync(user);
        return user;
    }

    private static ChatImage Photo(string type = "image/jpeg", long length = 3) => new(new MemoryStream(new byte[] { 1, 2, 3 }), "anh.jpg", type, length);

    // ------------------------------------------------------------------ chat 1-1

    [SkippableFact]
    public async Task Direct_chat_reuses_one_room_and_tracks_history_and_unread()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewUserAsync(s);
        var b = await NewUserAsync(s);
        var outsider = await NewUserAsync(s);
        var publisher = new RecordingPublisher();
        var chatA = As(s, a, publisher);
        var chatB = As(s, b, publisher);

        // Mở phòng: dùng lại phòng cũ dù ai mở trước; không nhắn cho chính mình / người không còn làm việc
        var room = await chatA.GetOrCreateDirectAsync(b.Id);
        Assert.Equal(room.Id, (await chatB.GetOrCreateDirectAsync(a.Id)).Id);
        Assert.Equal((ConversationType.Direct, b.FullName, b.Id), (room.Type, room.Title, room.OtherUser!.Id));
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.GetOrCreateDirectAsync(a.Id));
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.GetOrCreateDirectAsync(-5));
        var gone = await NewUserAsync(s, status: UserStatus.Suspended);
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.GetOrCreateDirectAsync(gone.Id));

        // Gửi tin: đẩy cho cả hai (kể cả thiết bị khác của người gửi); người nhận có 1 tin chưa đọc
        var first = await chatA.SendTextAsync(room.Id, new SendMessageRequest { Content = "  Xin chào  " });
        Assert.Equal(("Xin chào", a.Id, a.FullName), (first.Content, first.SenderId, first.SenderName));
        var (pushedTo, pushed) = Assert.Single(publisher.Created);
        Assert.Equal(new[] { a.Id, b.Id }, pushedTo.OrderBy(x => x));
        Assert.Equal(first.Id, pushed.Id);
        var listB = Assert.Single(await chatB.GetConversationsAsync());
        Assert.Equal((1, "Xin chào", a.Id), (listB.UnreadCount, listB.LastMessage!.Preview, listB.LastMessage.SenderId));
        Assert.Equal(0, Assert.Single(await chatA.GetConversationsAsync()).UnreadCount); // tin của chính mình không tính chưa đọc
        Assert.Equal(1, (await chatB.GetUnreadTotalAsync()).Count);

        // Lịch sử phân trang cũ → mới
        foreach (var text in new[] { "Hai", "Ba", "Bốn", "Năm" })
        {
            await chatB.SendTextAsync(room.Id, new SendMessageRequest { Content = text });
        }
        var latest = await chatA.GetMessagesAsync(room.Id, null, 2);
        Assert.Equal(new[] { "Bốn", "Năm" }, latest.Items.Select(m => m.Content));
        Assert.True(latest.HasMore);
        var older = await chatA.GetMessagesAsync(room.Id, latest.NextBeforeId, 2);
        Assert.Equal(new[] { "Hai", "Ba" }, older.Items.Select(m => m.Content));
        var oldest = await chatA.GetMessagesAsync(room.Id, older.NextBeforeId, 2);
        Assert.Equal(new[] { "Xin chào" }, oldest.Items.Select(m => m.Content));
        Assert.False(oldest.HasMore);
        Assert.Null(oldest.NextBeforeId);

        // Đã đọc: người gửi đọc xong thì số chưa đọc về 0, thiết bị khác của họ được báo
        Assert.Equal(4, Assert.Single(await chatA.GetConversationsAsync()).UnreadCount);
        var read = await chatA.MarkReadAsync(room.Id, null);
        Assert.Equal(0, read.UnreadCount);
        Assert.Contains(publisher.Read, r => r.UserId == a.Id && r.ConversationId == room.Id && r.Unread == 0);
        Assert.Equal(0, Assert.Single(await chatA.GetConversationsAsync()).UnreadCount);
        // B đã trả lời → coi như đã đọc hết tới tin của mình; tin mới của A thì lại chưa đọc
        Assert.Equal(0, Assert.Single(await chatB.GetConversationsAsync()).UnreadCount);
        await chatA.SendTextAsync(room.Id, new SendMessageRequest { Content = "Tin mới của A" });
        Assert.Equal(1, Assert.Single(await chatB.GetConversationsAsync()).UnreadCount);

        // Nội dung không hợp lệ
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.SendTextAsync(room.Id, new SendMessageRequest { Content = "   " }));
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.SendTextAsync(room.Id, new SendMessageRequest { Content = new string('x', ChatService.MaxContentLength + 1) }));

        // Người ngoài không đọc / gửi / đánh dấu được; phòng không tồn tại → 404
        var chatOutsider = As(s, outsider);
        await Assert.ThrowsAsync<ForbiddenException>(() => chatOutsider.GetMessagesAsync(room.Id, null, 10));
        await Assert.ThrowsAsync<ForbiddenException>(() => chatOutsider.SendTextAsync(room.Id, new SendMessageRequest { Content = "chen ngang" }));
        await Assert.ThrowsAsync<ForbiddenException>(() => chatOutsider.MarkReadAsync(room.Id, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => chatOutsider.GetConversationAsync(room.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => chatA.GetMessagesAsync(-1, null, 10));

        // Hub lỗi không làm hỏng việc gửi tin: tin đã lưu
        publisher.Throw = true;
        var saved = await chatA.SendTextAsync(room.Id, new SendMessageRequest { Content = "Vẫn gửi được" });
        Assert.Equal(saved.Id, (await chatB.GetMessagesAsync(room.Id, null, 1)).Items.Single().Id);
    }

    [SkippableFact]
    public async Task Two_people_opening_the_same_chat_at_once_get_one_room()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s1 = _db.CreateSession();
        await using var s2 = _db.CreateSession();
        var a = await NewUserAsync(s1);
        var b = await NewUserAsync(s1);

        var rooms = await Task.WhenAll(As(s1, a).GetOrCreateDirectAsync(b.Id), As(s2, b).GetOrCreateDirectAsync(a.Id));
        Assert.Equal(rooms[0].Id, rooms[1].Id);
        Assert.Equal(new[] { a.Id, b.Id }, await new ChatRepository(s1).GetMemberIdsAsync(rooms[0].Id));
    }

    // ------------------------------------------------------------------ ảnh & thu hồi

    [SkippableFact]
    public async Task Images_are_validated_and_recalled_messages_disappear_everywhere()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var a = await NewUserAsync(s);
        var b = await NewUserAsync(s);
        var publisher = new RecordingPublisher();
        var storage = new MemoryStorage();
        var chatA = As(s, a, publisher, storage);
        var chatB = As(s, b, publisher, storage);
        var room = await chatA.GetOrCreateDirectAsync(b.Id);

        await chatA.SendTextAsync(room.Id, new SendMessageRequest { Content = "Xem ảnh nhé" });
        var image = await chatA.SendImageAsync(room.Id, "Quầy sáng nay", Photo());
        Assert.StartsWith("https://img.test/chat/", image.ImageUrl);
        Assert.Equal("Quầy sáng nay", image.Content);
        Assert.Single(storage.Uploaded);
        var listB = Assert.Single(await chatB.GetConversationsAsync());
        Assert.Equal(("[Hình ảnh] Quầy sáng nay", 2), (listB.LastMessage!.Preview, listB.UnreadCount));
        Assert.Equal("[Hình ảnh]", (await chatA.SendImageAsync(room.Id, null, Photo())).Content is null ? "[Hình ảnh]" : "");

        // Ảnh không hợp lệ
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.SendImageAsync(room.Id, null, Photo("application/pdf")));
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.SendImageAsync(room.Id, null, Photo(length: ChatService.MaxImageBytes + 1)));
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.SendImageAsync(room.Id, null, Photo(length: 0)));
        Assert.Equal(2, storage.Uploaded.Count); // ảnh lỗi không được tải lên

        // Thu hồi: chỉ người gửi; tin biến mất ở mọi nơi, ảnh bị xóa, không tính chưa đọc
        await Assert.ThrowsAsync<ForbiddenException>(() => chatB.DeleteMessageAsync(image.Id));
        await chatA.DeleteMessageAsync(image.Id);
        var (to, conversationId, messageId) = Assert.Single(publisher.Deleted);
        Assert.Equal((room.Id, image.Id), (conversationId, messageId));
        Assert.Equal(new[] { a.Id, b.Id }, to.OrderBy(x => x));
        Assert.Contains(storage.Uploaded[0], storage.Deleted);
        await Assert.ThrowsAsync<BadRequestException>(() => chatA.DeleteMessageAsync(image.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => chatA.DeleteMessageAsync(-1));

        var history = await chatB.GetMessagesAsync(room.Id, null, 10);
        var recalled = history.Items.Single(m => m.Id == image.Id);
        Assert.True(recalled.IsDeleted);
        Assert.Null(recalled.Content);
        Assert.Null(recalled.ImageUrl);
        var after = Assert.Single(await chatB.GetConversationsAsync());
        Assert.Equal(2, after.UnreadCount); // 3 tin của A, trừ tin đã thu hồi
        Assert.Equal("[Hình ảnh]", after.LastMessage!.Preview); // tin ảnh thứ hai (không chú thích) mới là tin cuối còn hiệu lực
    }

    // ------------------------------------------------------------------ nhóm chi nhánh

    [SkippableFact]
    public async Task Branch_groups_sync_members_and_follow_branch_changes()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var branchX = await NewBranchAsync(s);
        var branchY = await NewBranchAsync(s);
        var e1 = await NewUserAsync(s, branchX);
        var e2 = await NewUserAsync(s, branchX);
        var e3 = await NewUserAsync(s, branchY);
        var hr = await NewUserAsync(s, null);
        var publisher = new RecordingPublisher();

        // Mở chat lần đầu → nhóm chi nhánh tự tạo, thêm cả nhân sự chưa từng mở chat
        var groupX = Assert.Single(await As(s, e1, publisher).GetConversationsAsync(), c => c.BranchId == branchX);
        Assert.Equal((ConversationType.Group, 2), (groupX.Type, groupX.MemberCount));
        Assert.StartsWith("Chi nhánh chat", groupX.Title);
        Assert.Equal(0, Assert.Single(await As(s, e2).GetConversationsAsync(), c => c.Id == groupX.Id).UnreadCount);
        Assert.DoesNotContain(await As(s, e3).GetConversationsAsync(), c => c.Id == groupX.Id); // chi nhánh khác có nhóm riêng
        Assert.Empty(await As(s, hr).GetConversationsAsync()); // không thuộc chi nhánh nào → không có nhóm tự động

        // Gửi tin vào nhóm: đẩy cho đúng thành viên, người ngoài không đọc / gửi được
        await As(s, e1, publisher).SendTextAsync(groupX.Id, new SendMessageRequest { Content = "Họp giao ban 9h" });
        Assert.Equal(new[] { e1.Id, e2.Id }, publisher.Created.Single().Users.OrderBy(x => x));
        Assert.Equal(1, Assert.Single(await As(s, e2).GetConversationsAsync(), c => c.Id == groupX.Id).UnreadCount);
        await Assert.ThrowsAsync<ForbiddenException>(() => As(s, e3).GetMessagesAsync(groupX.Id, null, 10));
        await Assert.ThrowsAsync<ForbiddenException>(() => As(s, e3).SendTextAsync(groupX.Id, new SendMessageRequest { Content = "x" }));

        // Nhân sự mới vào chi nhánh: vào nhóm khi mở chat, lịch sử cũ không bị tính là chưa đọc
        var e4 = await NewUserAsync(s, branchX);
        var groupForE4 = Assert.Single(await As(s, e4).GetConversationsAsync(), c => c.BranchId == branchX);
        Assert.Equal((groupX.Id, 0, 3), (groupForE4.Id, groupForE4.UnreadCount, groupForE4.MemberCount));
        Assert.Equal(new[] { "Họp giao ban 9h" }, (await As(s, e4).GetMessagesAsync(groupX.Id, null, 10)).Items.Select(m => m.Content));
        Assert.Equal(3, (await As(s, e1).GetConversationAsync(groupX.Id)).Members.Count);

        // Chuyển chi nhánh: mất quyền ngay (kể cả khi chưa đồng bộ), rời nhóm cũ khi mở chat, vào nhóm chi nhánh mới
        var users = new UserRepository(s);
        e2.BranchId = branchY;
        await users.UpdateAsync(e2);
        await Assert.ThrowsAsync<ForbiddenException>(() => As(s, e2).GetMessagesAsync(groupX.Id, null, 10));
        var afterMove = await As(s, e2).GetConversationsAsync();
        Assert.DoesNotContain(afterMove, c => c.Id == groupX.Id);
        Assert.Contains(afterMove, c => c.BranchId == branchY && c.MemberCount == 2);
        Assert.Equal(2, (await As(s, e1).GetConversationsAsync()).Single(c => c.Id == groupX.Id).MemberCount);

        // Nhân sự nghỉ việc bị bỏ khỏi nhóm ở lần đồng bộ sau
        e4.Status = UserStatus.Suspended;
        await users.UpdateAsync(e4);
        Assert.Equal(1, (await As(s, e1).GetConversationsAsync()).Single(c => c.Id == groupX.Id).MemberCount);
    }

    // ------------------------------------------------------------------ danh bạ

    [SkippableFact]
    public async Task Contacts_list_active_colleagues_except_myself()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var branch = await NewBranchAsync(s);
        var me = await NewUserAsync(s, branch);
        var colleague = await NewUserAsync(s, branch);
        var left = await NewUserAsync(s, branch, UserStatus.Left);
        var chat = As(s, me);

        var found = await chat.GetContactsAsync(new ChatContactFilter { Search = colleague.EmployeeCode.ToLowerInvariant() });
        var contact = Assert.Single(found.Items);
        Assert.Equal((colleague.Id, colleague.FullName), (contact.Id, contact.FullName));
        Assert.StartsWith("Chi nhánh chat", contact.BranchName);
        Assert.Empty((await chat.GetContactsAsync(new ChatContactFilter { Search = me.EmployeeCode })).Items); // không tự nhắn cho mình
        Assert.Empty((await chat.GetContactsAsync(new ChatContactFilter { Search = left.EmployeeCode })).Items); // đã nghỉ việc
        Assert.True((await chat.GetContactsAsync(new ChatContactFilter { PageSize = 1000 })).PageSize <= ChatService.MaxContactPageSize);
    }
}
