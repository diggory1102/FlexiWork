using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Security;
using FW.Application.Common.Settings;
using FW.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
/// sang khuôn mặt người khác để nhờ chấm công hộ. Khuôn mặt giống một nhân viên đang làm khác (từ
/// <see cref="FaceRecognitionSettings.DuplicateThreshold"/>) bị từ chối để hai tài khoản không dùng chung một người.
/// Vector đặc trưng không bao giờ trả về client.
/// </summary>
public class FaceProfileService : IFaceProfileService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public static readonly IReadOnlyList<string> AllowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };
    public const string StorageFolder = "faces";

    private readonly FaceRecognitionSettings _settings;
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
        IOptions<FaceRecognitionSettings> settings,
        ILogger<FaceProfileService> logger)
    {
        _settings = settings.Value;
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

        await EnsureNotRegisteredByAnotherAsync(userId, extracted.Embedding, cancellationToken);

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

    /// <summary>
    /// Chống hai tài khoản dùng chung một khuôn mặt (nhờ chấm công hộ): so vector mới với khuôn mặt của mọi nhân viên đang làm khác.
    /// Không nêu tên người trùng cho nhân viên (bảo mật thông tin); chỉ ghi log để Nhân sự tra cứu.
    /// </summary>
    private async Task EnsureNotRegisteredByAnotherAsync(int userId, IReadOnlyList<float> embedding, CancellationToken cancellationToken)
    {
        var threshold = _settings.EffectiveDuplicateThreshold;
        var others = await _profiles.GetOtherActiveProfilesAsync(userId, cancellationToken);
        FaceProfile? closest = null;
        var best = double.MinValue;
        foreach (var other in others)
        {
            var similarity = FaceSimilarity.Cosine(embedding, other.FaceEncoding);
            if (similarity > best)
            {
                best = similarity;
                closest = other;
            }
        }

        if (closest == null || best < threshold) return;

        _logger.LogWarning("Chặn đăng ký khuôn mặt trùng: nhân viên {UserId} giống nhân viên {OtherUserId} (độ giống {Similarity:0.000} ≥ ngưỡng {Threshold:0.00})",
            userId, closest.UserId, best, threshold);
        throw new BadRequestException("Khuôn mặt này đã được đăng ký cho một tài khoản khác nên không thể dùng cho tài khoản của bạn. " +
                                      "Nếu đây là nhầm lẫn, hãy liên hệ Quản lý hoặc bộ phận Nhân sự.");
    }

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
