using FW.Application.Common.Exceptions;
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

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
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
}
