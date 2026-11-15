using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FW.Application.Features.FaceId;

/// <summary>Trạng thái đăng ký khuôn mặt của một nhân viên (không chứa vector đặc trưng).</summary>
public record FaceProfileStatusDto(bool Registered, string? SampleImageUrl, string? ModelName, DateTime? RegisteredAt, DateTime? UpdatedAt)
{
    public static readonly FaceProfileStatusDto NotRegistered = new(false, null, null, null, null);

    public static FaceProfileStatusDto From(FaceProfile p) => new(true, p.SampleImageUrl, p.ModelName, p.CreatedAt, p.UpdatedAt);
}

public interface IFaceProfileService
{
    Task<FaceProfileStatusDto> GetMyStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Nhân viên tự đăng ký khuôn mặt (chỉ lần đầu — đổi lại phải nhờ Quản lý/HR đặt lại).</summary>
    Task<FaceProfileStatusDto> RegisterMineAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default);

    Task<FaceProfileStatusDto> GetStatusAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Quản lý/HR xóa dữ liệu khuôn mặt để nhân viên đăng ký lại (VD đăng ký sai, đổi diện mạo).</summary>
    Task ResetAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đăng ký dữ liệu Face ID (UC 2.4): ảnh mẫu → trích vector đặc trưng 512 chiều → lưu <c>face_profiles</c>, ảnh lưu kho ảnh.
/// Chống gian lận: nhân viên chỉ tự đăng ký khi chưa có dữ liệu; muốn thay phải được Quản lý/HR đặt lại, tránh việc tự đổi
/// sang khuôn mặt người khác để nhờ chấm công hộ. Vector đặc trưng không bao giờ trả về client.
/// </summary>
public class FaceProfileService : IFaceProfileService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public static readonly IReadOnlyList<string> AllowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };
    public const string StorageFolder = "faces";

    private readonly IFaceProfileRepository _profiles;
    private readonly IUserRepository _users;
    private readonly IFaceRecognitionService _recognition;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FaceProfileService> _logger;

    public FaceProfileService(
        IFaceProfileRepository profiles,
        IUserRepository users,
        IFaceRecognitionService recognition,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        ILogger<FaceProfileService> logger)
    {
        _profiles = profiles;
        _users = users;
        _recognition = recognition;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<FaceProfileStatusDto> GetMyStatusAsync(CancellationToken cancellationToken = default) =>
        StatusOfAsync(CurrentUserId, cancellationToken);

    public async Task<FaceProfileStatusDto> RegisterMineAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0) throw new BadRequestException("Vui lòng chụp ảnh khuôn mặt.");
        if (length > MaxImageBytes) throw new BadRequestException("Ảnh tối đa 5 MB.");
        if (!AllowedTypes.Contains(contentType.ToLowerInvariant())) throw new BadRequestException("Ảnh phải là JPG, PNG, WEBP hoặc HEIC.");

        var userId = CurrentUserId;
        if (await _profiles.GetByUserIdAsync(userId, cancellationToken) != null)
        {
            throw new BadRequestException("Bạn đã đăng ký khuôn mặt. Muốn đăng ký lại, hãy nhờ Quản lý hoặc bộ phận Nhân sự đặt lại dữ liệu khuôn mặt.");
        }

        // Ảnh dùng 2 lần (trích đặc trưng + lưu kho ảnh) nên đọc vào bộ nhớ một lần
        using var image = new MemoryStream();
        await content.CopyToAsync(image, cancellationToken);

        image.Position = 0;
        var extracted = await _recognition.ExtractEmbeddingAsync(image, fileName, cancellationToken);
        if (!extracted.Success || extracted.Embedding is not { Count: > 0 })
        {
            throw new BadRequestException(extracted.Message);
        }

        image.Position = 0;
        var uploaded = await _storage.UploadImageAsync(image, fileName, StorageFolder, cancellationToken);
        var profile = new FaceProfile
        {
            UserId = userId,
            FaceEncoding = extracted.Embedding.ToArray(),
            SampleImageUrl = uploaded.Url,
            SampleImagePublicId = uploaded.PublicId,
            ModelName = FaceProfile.DefaultModelName
        };

        try
        {
            await _profiles.UpsertAsync(profile, cancellationToken);
        }
        catch
        {
            await TryDeleteImageAsync(uploaded.PublicId);
            throw;
        }

        return await StatusOfAsync(userId, cancellationToken);
    }

    public async Task<FaceProfileStatusDto> GetStatusAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureManageableAsync(userId, cancellationToken);
        return await StatusOfAsync(userId, cancellationToken);
    }

    public async Task ResetAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureManageableAsync(userId, cancellationToken);
        var profile = await _profiles.GetByUserIdAsync(userId, cancellationToken)
                      ?? throw new BadRequestException("Nhân viên chưa đăng ký khuôn mặt.");

        await _profiles.DeleteByUserIdAsync(userId, cancellationToken);
        await TryDeleteImageAsync(profile.SampleImagePublicId);
    }

    // ------------------------------------------------------------------

    private int CurrentUserId => _currentUser.UserId ?? throw new UnauthorizedException();

    private async Task<FaceProfileStatusDto> StatusOfAsync(int userId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByUserIdAsync(userId, cancellationToken);
        return profile == null ? FaceProfileStatusDto.NotRegistered : FaceProfileStatusDto.From(profile);
    }

    /// <summary>Admin/HR: mọi nhân viên; Quản lý: nhân viên thuộc chi nhánh mình.</summary>
    private async Task EnsureManageableAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Nhân viên", userId);
        if (!_currentUser.HasCompanyWideAccess()) _currentUser.EnsureCanAccessBranch(user.BranchId);
    }

    /// <summary>Ảnh cũ trên kho ảnh: xóa không được thì chỉ ghi log (dữ liệu khuôn mặt đã được xử lý xong).</summary>
    private async Task TryDeleteImageAsync(string? publicId)
    {
        if (string.IsNullOrEmpty(publicId)) return;
        try
        {
            await _storage.DeleteAsync(publicId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được ảnh khuôn mặt {PublicId} trên kho ảnh", publicId);
        }
    }
}
