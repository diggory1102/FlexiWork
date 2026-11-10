using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;

namespace FW.Application.Features.Users;

public interface IProfileService
{
    Task<UserDto> GetMyProfileAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateMyAvatarAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hồ sơ cá nhân: mọi người dùng đã đăng nhập tự xem hồ sơ, sửa số điện thoại và ảnh đại diện.
/// </summary>
public class ProfileService : IProfileService
{
    public const long MaxAvatarBytes = 5 * 1024 * 1024;
    public static readonly IReadOnlyList<string> AllowedAvatarTypes = new[] { "image/jpeg", "image/png", "image/webp" };

    private readonly IUserRepository _users;
    private readonly IUserService _userService;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public ProfileService(IUserRepository users, IUserService userService, IFileStorageService storage, ICurrentUserService currentUser)
    {
        _users = users;
        _userService = userService;
        _storage = storage;
        _currentUser = currentUser;
    }

    public Task<UserDto> GetMyProfileAsync(CancellationToken cancellationToken = default) =>
        _userService.GetByIdAsync(CurrentUserId, cancellationToken);

    public async Task<UserDto> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(CurrentUserId, cancellationToken) ?? throw new UnauthorizedException();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await _users.UpdateAsync(user, cancellationToken);
        return await GetMyProfileAsync(cancellationToken);
    }

    public async Task<UserDto> UpdateMyAvatarAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0) throw new BadRequestException("Vui lòng chọn ảnh đại diện.");
        if (length > MaxAvatarBytes) throw new BadRequestException("Ảnh đại diện tối đa 5 MB.");
        if (!AllowedAvatarTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new BadRequestException("Ảnh đại diện phải là JPG, PNG hoặc WEBP.");
        }

        var user = await _users.GetByIdAsync(CurrentUserId, cancellationToken) ?? throw new UnauthorizedException();
        var uploaded = await _storage.UploadImageAsync(content, fileName, "avatars", cancellationToken);

        user.AvatarUrl = uploaded.Url;
        await _users.UpdateAsync(user, cancellationToken);
        return await GetMyProfileAsync(cancellationToken);
    }

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();
}
