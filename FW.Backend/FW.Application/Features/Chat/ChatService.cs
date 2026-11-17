using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;
using FW.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.Chat;

/// <summary>Ảnh gửi kèm tin nhắn.</summary>
public record ChatImage(Stream Content, string FileName, string ContentType, long Length);

public interface IChatService
{
    /// <summary>Phòng chat của tôi (hoạt động gần nhất trước). Mỗi lần gọi đồng bộ nhóm chi nhánh của tôi (UC 7.3).</summary>
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(CancellationToken cancellationToken = default);

    Task<ConversationDetailDto> GetConversationAsync(int conversationId, CancellationToken cancellationToken = default);

    /// <summary>Mở chat trực tiếp với một người — dùng lại phòng đã có (UC 7.1).</summary>
    Task<ConversationDto> GetOrCreateDirectAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử tin nhắn, cũ → mới; trang cũ hơn: <paramref name="beforeId"/> = <see cref="MessagePageDto.NextBeforeId"/>.</summary>
    Task<MessagePageDto> GetMessagesAsync(int conversationId, long? beforeId, int? pageSize, CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendTextAsync(int conversationId, SendMessageRequest request, CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendImageAsync(int conversationId, string? caption, ChatImage image, CancellationToken cancellationToken = default);

    Task<ChatUnreadDto> MarkReadAsync(int conversationId, long? upToMessageId, CancellationToken cancellationToken = default);

    /// <summary>Thu hồi tin nhắn của chính mình.</summary>
    Task DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default);

    Task<ChatTotalUnreadDto> GetUnreadTotalAsync(CancellationToken cancellationToken = default);

    /// <summary>Tìm người để nhắn tin (mọi nhân sự đang hoạt động trừ chính mình).</summary>
    Task<PaginatedResult<ChatContactDto>> GetContactsAsync(ChatContactFilter filter, CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ Nhóm tùy chỉnh (UC 7.2)

    /// <summary>Tạo nhóm chat tùy chỉnh: tôi là quản trị nhóm.</summary>
    Task<ConversationDto> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Đổi tên nhóm (quản trị nhóm).</summary>
    Task<ConversationDto> RenameGroupAsync(int conversationId, RenameGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Thêm thành viên (quản trị nhóm). Người đã có trong nhóm được bỏ qua.</summary>
    Task<ConversationDetailDto> AddMembersAsync(int conversationId, AddMembersRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xóa một thành viên khỏi nhóm (quản trị nhóm), hoặc tự rời nhóm khi <paramref name="userId"/> là chính mình.</summary>
    Task RemoveMemberAsync(int conversationId, int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Chat nội bộ (Task 5.2, UC 7.1, 7.3): chat trực tiếp 1-1 và nhóm tự động của từng chi nhánh, tin nhắn văn bản / ảnh, lịch sử phân trang,
/// số chưa đọc theo từng người, thu hồi tin. Chỉ thành viên mới đọc / gửi được; nhóm chi nhánh chỉ dành cho người đang thuộc chi nhánh
/// đó (đổi chi nhánh là mất quyền ngay, kể cả khi chưa kịp đồng bộ danh sách thành viên). Nhóm tùy chỉnh (UC 7.2) là phần tùy chọn.
/// </summary>
public class ChatService : IChatService
{
    public const int MaxContentLength = 2000;
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 100;
    public const int MaxContactPageSize = 50;
    public const int MaxGroupTitleLength = 100;
    public const int MaxGroupMembers = 100;
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const string StorageFolder = "chat";
    private static readonly IReadOnlyList<string> AllowedImageTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };

    private readonly IChatRepository _chat;
    private readonly IUserRepository _users;
    private readonly IBranchRepository _branches;
    private readonly IPositionRepository _positions;
    private readonly IChatPublisher _publisher;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IChatRepository chat, IUserRepository users, IBranchRepository branches, IPositionRepository positions,
        IChatPublisher publisher, IFileStorageService storage, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<ChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _chat = chat;
        _users = users;
        _branches = branches;
        _positions = positions;
        _publisher = publisher;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    // ------------------------------------------------------------------ Phòng chat

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        await SyncMyBranchGroupAsync(cancellationToken);
        return (await _chat.GetConversationRowsAsync(CurrentUserId, cancellationToken)).Select(ConversationDto.From).ToList();
    }

    public async Task<ConversationDetailDto> GetConversationAsync(int conversationId, CancellationToken cancellationToken = default)
    {
        await RequireMemberAsync(conversationId, cancellationToken);
        var row = await _chat.GetConversationRowAsync(conversationId, CurrentUserId, cancellationToken) ?? throw new NotFoundException("Phòng chat", conversationId);
        var members = await _chat.GetMembersAsync(conversationId, cancellationToken);
        return new ConversationDetailDto(ConversationDto.From(row),
            members.Select(m => new ChatMemberDto(m.UserId, m.EmployeeCode, m.FullName, m.AvatarUrl, m.PositionTitle)).ToList());
    }

    public async Task<ConversationDto> GetOrCreateDirectAsync(int userId, CancellationToken cancellationToken = default)
    {
        var me = CurrentUserId;
        if (userId == me) throw new BadRequestException("Không thể nhắn tin cho chính mình.");
        var other = await _users.GetByIdAsync(userId, cancellationToken);
        if (other is null || other.Status != UserStatus.Active) throw new BadRequestException("Người này không tồn tại hoặc đã ngừng làm việc.");

        var id = await _chat.GetOrCreateDirectAsync(me, userId, cancellationToken);
        var row = await _chat.GetConversationRowAsync(id, me, cancellationToken) ?? throw new NotFoundException("Phòng chat", id);
        return ConversationDto.From(row);
    }

    // ------------------------------------------------------------------ Tin nhắn

    public async Task<MessagePageDto> GetMessagesAsync(int conversationId, long? beforeId, int? pageSize, CancellationToken cancellationToken = default)
    {
        await RequireMemberAsync(conversationId, cancellationToken);
        var take = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var rows = await _chat.GetMessagesAsync(conversationId, beforeId, take + 1, cancellationToken); // mới nhất trước
        var hasMore = rows.Count > take;
        var page = rows.Take(take).Reverse().Select(ChatMessageDto.From).ToList(); // đổi sang cũ → mới
        return new MessagePageDto(page, hasMore, hasMore ? page[0].Id : null);
    }

    public async Task<ChatMessageDto> SendTextAsync(int conversationId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0) throw new BadRequestException("Vui lòng nhập nội dung tin nhắn.");
        if (content.Length > MaxContentLength) throw new BadRequestException($"Tin nhắn tối đa {MaxContentLength} ký tự.");
        await RequireMemberAsync(conversationId, cancellationToken);

        return await SaveAndPublishAsync(new Message { ConversationId = conversationId, SenderId = CurrentUserId, Content = content }, cancellationToken);
    }

    public async Task<ChatMessageDto> SendImageAsync(int conversationId, string? caption, ChatImage image, CancellationToken cancellationToken = default)
    {
        var text = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (text?.Length > MaxContentLength) throw new BadRequestException($"Chú thích tối đa {MaxContentLength} ký tự.");
        if (image.Length <= 0) throw new BadRequestException("Ảnh bị rỗng.");
        if (image.Length > MaxImageBytes) throw new BadRequestException("Ảnh tối đa 5 MB.");
        if (!AllowedImageTypes.Contains(image.ContentType.ToLowerInvariant())) throw new BadRequestException("Ảnh phải là JPG, PNG, WEBP hoặc HEIC.");
        await RequireMemberAsync(conversationId, cancellationToken);

        var uploaded = await _storage.UploadImageAsync(image.Content, image.FileName, StorageFolder, cancellationToken);
        try
        {
            return await SaveAndPublishAsync(new Message
            {
                ConversationId = conversationId, SenderId = CurrentUserId, Content = text, ImageUrl = uploaded.Url, ImagePublicId = uploaded.PublicId
            }, cancellationToken);
        }
        catch
        {
            await TryDeleteImageAsync(uploaded.PublicId);
            throw;
        }
    }

    private async Task<ChatMessageDto> SaveAndPublishAsync(Message message, CancellationToken cancellationToken)
    {
        var id = await _chat.AddAsync(message, cancellationToken);
        // Người vừa gửi tin coi như đã đọc tới tin này
        await _chat.MarkReadAsync(message.ConversationId, message.SenderId, id, cancellationToken);
        var dto = ChatMessageDto.From(await _chat.GetMessageAsync(id, cancellationToken) ?? throw new NotFoundException("Tin nhắn", id));

        try
        {
            await _publisher.MessageCreatedAsync(await _chat.GetMemberIdsAsync(message.ConversationId, cancellationToken), dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đẩy được tin nhắn {MessageId} của phòng {ConversationId}", id, message.ConversationId);
        }
        return dto;
    }

    public async Task<ChatUnreadDto> MarkReadAsync(int conversationId, long? upToMessageId, CancellationToken cancellationToken = default)
    {
        await RequireMemberAsync(conversationId, cancellationToken);
        var me = CurrentUserId;
        var unread = await _chat.MarkReadAsync(conversationId, me, upToMessageId, cancellationToken);
        try
        {
            await _publisher.ConversationReadAsync(me, conversationId, unread, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đồng bộ được trạng thái đã đọc của phòng {ConversationId}", conversationId);
        }
        return new ChatUnreadDto(conversationId, unread);
    }

    public async Task DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default)
    {
        var message = await _chat.GetMessageAsync(messageId, cancellationToken) ?? throw new NotFoundException("Tin nhắn", messageId);
        if (message.SenderId != CurrentUserId) throw new ForbiddenException("Bạn chỉ thu hồi được tin nhắn của chính mình.");
        if (message.IsDeleted) throw new BadRequestException("Tin nhắn đã được thu hồi.");
        if (!await _chat.SoftDeleteMessageAsync(messageId, message.SenderId, cancellationToken)) throw new BadRequestException("Tin nhắn đã được thu hồi.");

        if (message.ImagePublicId is not null) await TryDeleteImageAsync(message.ImagePublicId);
        try
        {
            await _publisher.MessageDeletedAsync(await _chat.GetMemberIdsAsync(message.ConversationId, cancellationToken), message.ConversationId, messageId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đẩy được sự kiện thu hồi tin nhắn {MessageId}", messageId);
        }
    }

    public async Task<ChatTotalUnreadDto> GetUnreadTotalAsync(CancellationToken cancellationToken = default) =>
        new(await _chat.CountUnreadTotalAsync(CurrentUserId, cancellationToken));

    // ------------------------------------------------------------------ Danh bạ

    public async Task<PaginatedResult<ChatContactDto>> GetContactsAsync(ChatContactFilter filter, CancellationToken cancellationToken = default)
    {
        var me = CurrentUserId;
        var page = await _users.GetPagedAsync(new UserFilter
        {
            Search = filter.Search, Status = UserStatus.Active, PageIndex = filter.PageIndex, PageSize = Math.Min(filter.NormalizedPageSize, MaxContactPageSize)
        }, cancellationToken);

        var branches = (await _branches.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        var positions = (await _positions.GetAllAsync(cancellationToken)).ToDictionary(p => p.Id, p => p.Title);

        // Bỏ chính mình khỏi trang (tổng số được trừ tương ứng)
        var contacts = page.Items.Where(u => u.Id != me).Select(u => new ChatContactDto(u.Id, u.EmployeeCode, u.FullName, u.AvatarUrl,
            u.BranchId is { } b && branches.TryGetValue(b, out var branch) ? branch : null,
            u.PositionId is { } p && positions.TryGetValue(p, out var position) ? position : null)).ToList();
        return PaginatedResult<ChatContactDto>.Create(contacts, page.TotalCount - (page.Items.Count - contacts.Count), page.PageIndex, page.PageSize);
    }

    // ------------------------------------------------------------------ Nhóm tùy chỉnh (UC 7.2)

    public async Task<ConversationDto> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        var me = CurrentUserId;
        var title = NormalizeGroupTitle(request.Title);
        var memberIds = request.MemberIds.Where(id => id != me).Distinct().ToList();
        if (memberIds.Count == 0) throw new BadRequestException("Nhóm cần có ít nhất một thành viên khác ngoài bạn.");
        if (memberIds.Count + 1 > MaxGroupMembers) throw new BadRequestException($"Nhóm tối đa {MaxGroupMembers} thành viên.");
        await EnsureActiveUsersAsync(memberIds, cancellationToken);

        var id = await _unitOfWork.ExecuteInTransactionAsync(ct => _chat.CreateGroupAsync(title, me, memberIds, ct), cancellationToken);
        await PublishUpdatedAsync(memberIds.Append(me).ToList(), id);
        return ConversationDto.From(await _chat.GetConversationRowAsync(id, me, cancellationToken) ?? throw new NotFoundException("Phòng chat", id));
    }

    public async Task<ConversationDto> RenameGroupAsync(int conversationId, RenameGroupRequest request, CancellationToken cancellationToken = default)
    {
        await RequireGroupOwnerAsync(conversationId, cancellationToken);
        await _chat.RenameGroupAsync(conversationId, NormalizeGroupTitle(request.Title), cancellationToken);
        await PublishUpdatedAsync(await _chat.GetMemberIdsAsync(conversationId, cancellationToken), conversationId);
        return ConversationDto.From(await _chat.GetConversationRowAsync(conversationId, CurrentUserId, cancellationToken) ?? throw new NotFoundException("Phòng chat", conversationId));
    }

    public async Task<ConversationDetailDto> AddMembersAsync(int conversationId, AddMembersRequest request, CancellationToken cancellationToken = default)
    {
        await RequireGroupOwnerAsync(conversationId, cancellationToken);
        var existing = await _chat.GetMemberIdsAsync(conversationId, cancellationToken);
        var newIds = request.UserIds.Distinct().Where(id => !existing.Contains(id)).ToList();
        if (newIds.Count == 0) throw new BadRequestException("Những người này đã ở trong nhóm.");
        if (existing.Count + newIds.Count > MaxGroupMembers) throw new BadRequestException($"Nhóm tối đa {MaxGroupMembers} thành viên.");
        await EnsureActiveUsersAsync(newIds, cancellationToken);

        await _chat.AddMembersAsync(conversationId, newIds, cancellationToken);
        await PublishUpdatedAsync(await _chat.GetMemberIdsAsync(conversationId, cancellationToken), conversationId);
        return await GetConversationAsync(conversationId, cancellationToken);
    }

    public async Task RemoveMemberAsync(int conversationId, int userId, CancellationToken cancellationToken = default)
    {
        var me = CurrentUserId;
        var group = await RequireMemberAsync(conversationId, cancellationToken);
        if (!group.IsCustomGroup) throw new BadRequestException("Chỉ nhóm chat tùy chỉnh mới thêm / xóa thành viên được; nhóm chi nhánh do hệ thống quản lý.");
        var before = await _chat.GetMemberIdsAsync(conversationId, cancellationToken);

        if (userId != me)
        {
            if (group.CreatedBy != me) throw new ForbiddenException("Chỉ quản trị nhóm mới xóa được thành viên khác.");
            if (!before.Contains(userId)) throw new BadRequestException("Người này không ở trong nhóm.");
            await _chat.RemoveMemberAsync(conversationId, userId, cancellationToken);
        }
        else
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _chat.RemoveMemberAsync(conversationId, me, ct);
                if (group.CreatedBy != me) return;
                // Quản trị nhóm rời đi: người vào nhóm sớm nhất kế nhiệm; hết người thì đóng nhóm
                if (await _chat.GetOldestMemberAsync(conversationId, me, ct) is { } successor) await _chat.SetGroupOwnerAsync(conversationId, successor, ct);
                else await _chat.DeleteConversationAsync(conversationId, ct);
            }, cancellationToken);
        }

        await PublishUpdatedAsync(before, conversationId); // gồm cả người vừa rời / bị xóa để thiết bị của họ bỏ phòng khỏi danh sách
    }

    private static string NormalizeGroupTitle(string? title)
    {
        var value = title?.Trim() ?? string.Empty;
        if (value.Length < 2 || value.Length > MaxGroupTitleLength) throw new BadRequestException($"Tên nhóm từ 2 đến {MaxGroupTitleLength} ký tự.");
        return value;
    }

    private async Task EnsureActiveUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken)
    {
        foreach (var id in userIds)
        {
            var user = await _users.GetByIdAsync(id, cancellationToken);
            if (user is null || user.Status != UserStatus.Active) throw new BadRequestException("Có người không tồn tại hoặc đã ngừng làm việc.");
        }
    }

    /// <summary>Phòng phải là nhóm tùy chỉnh và tôi là quản trị nhóm.</summary>
    private async Task<Conversation> RequireGroupOwnerAsync(int conversationId, CancellationToken cancellationToken)
    {
        var group = await RequireMemberAsync(conversationId, cancellationToken);
        if (!group.IsCustomGroup) throw new BadRequestException("Chỉ nhóm chat tùy chỉnh mới đổi tên / thêm thành viên được; nhóm chi nhánh do hệ thống quản lý.");
        if (group.CreatedBy != CurrentUserId) throw new ForbiddenException("Chỉ quản trị nhóm (người tạo nhóm) mới thực hiện được thao tác này.");
        return group;
    }

    private async Task PublishUpdatedAsync(IReadOnlyCollection<int> userIds, int conversationId)
    {
        try
        {
            await _publisher.ConversationUpdatedAsync(userIds, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đẩy được sự kiện cập nhật nhóm chat {ConversationId}", conversationId);
        }
    }

    // ------------------------------------------------------------------ Tiện ích

    /// <summary>Đảm bảo nhóm chat của chi nhánh tôi tồn tại và đúng thành viên; rời các nhóm chi nhánh cũ.</summary>
    private async Task SyncMyBranchGroupAsync(CancellationToken cancellationToken)
    {
        var me = await _users.GetByIdAsync(CurrentUserId, cancellationToken) ?? throw new UnauthorizedException();
        await _chat.LeaveOtherBranchGroupsAsync(me.Id, me.BranchId, cancellationToken);
        if (me.BranchId is not { } branchId) return;

        var branch = await _branches.GetByIdAsync(branchId, cancellationToken);
        if (branch is null) return;
        await _chat.SyncBranchGroupAsync(branchId, branch.Name, cancellationToken);
    }

    /// <summary>Phòng phải tồn tại và tôi phải là thành viên; nhóm chi nhánh còn đòi hỏi tôi đang thuộc chi nhánh đó.</summary>
    private async Task<Conversation> RequireMemberAsync(int conversationId, CancellationToken cancellationToken)
    {
        var conversation = await _chat.GetConversationAsync(conversationId, cancellationToken) ?? throw new NotFoundException("Phòng chat", conversationId);
        if (!await _chat.IsMemberAsync(conversationId, CurrentUserId, cancellationToken)) throw new ForbiddenException("Bạn không thuộc phòng chat này.");
        if (conversation.Type == ConversationType.Group && conversation.BranchId.HasValue)
        {
            // Đọc chi nhánh hiện tại từ CSDL (token có thể cũ): đã chuyển chi nhánh là mất quyền ngay, dù danh sách thành viên chưa kịp đồng bộ
            var me = await _users.GetByIdAsync(CurrentUserId, cancellationToken);
            if (me?.BranchId != conversation.BranchId) throw new ForbiddenException("Bạn không còn thuộc chi nhánh của nhóm chat này.");
        }
        return conversation;
    }

    private async Task TryDeleteImageAsync(string publicId)
    {
        try
        {
            await _storage.DeleteAsync(publicId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được ảnh chat {PublicId}", publicId);
        }
    }
}
