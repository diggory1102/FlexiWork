using FW.Application.Common.Exceptions;
using FW.Application.Features.FaceId;
using FW.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Hồ sơ cá nhân của người đang đăng nhập (mọi vai trò).
/// </summary>
[Authorize]
public class ProfileController : ApiControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IFaceProfileService _faceProfileService;

    public ProfileController(IProfileService profileService, IFaceProfileService faceProfileService)
    {
        _profileService = profileService;
        _faceProfileService = faceProfileService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Success(await _profileService.GetMyProfileAsync(cancellationToken));

    /// <summary>Tự cập nhật số điện thoại (các thông tin khác do bộ phận Nhân sự quản lý).</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateMyProfileRequest request, CancellationToken cancellationToken) =>
        Success(await _profileService.UpdateMyProfileAsync(request, cancellationToken), "Cập nhật hồ sơ thành công.");

    /// <summary>Tải ảnh đại diện (JPG/PNG/WEBP, tối đa 5 MB) — form-data, trường "file".</summary>
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileService.MaxAvatarBytes + 64 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null) throw new BadRequestException("Vui lòng chọn ảnh đại diện (trường form-data \"file\").");

        await using var stream = file.OpenReadStream();
        var profile = await _profileService.UpdateMyAvatarAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return Success(profile, "Cập nhật ảnh đại diện thành công.");
    }

    /// <summary>Trạng thái đăng ký khuôn mặt (Face ID) của tôi.</summary>
    [HttpGet("face")]
    public async Task<IActionResult> GetFace(CancellationToken cancellationToken) =>
        Success(await _faceProfileService.GetMyStatusAsync(cancellationToken));

    /// <summary>
    /// Đăng ký khuôn mặt mẫu (form-data, trường "file"; JPG/PNG/WEBP/HEIC, tối đa 5 MB; chụp một mình, chính diện).
    /// Chỉ đăng ký được khi chưa có dữ liệu — muốn đổi phải nhờ Quản lý/HR đặt lại.
    /// </summary>
    [HttpPost("face")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FaceProfileService.MaxImageBytes + 64 * 1024)]
    public async Task<IActionResult> RegisterFace(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null) throw new BadRequestException("Vui lòng chụp ảnh khuôn mặt (trường form-data \"file\").");

        await using var stream = file.OpenReadStream();
        var status = await _faceProfileService.RegisterMineAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return Created(status, "Đăng ký khuôn mặt thành công.");
    }
}
