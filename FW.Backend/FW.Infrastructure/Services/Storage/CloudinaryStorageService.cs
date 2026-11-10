using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace FW.Infrastructure.Services.Storage;

/// <summary>
/// Lưu trữ ảnh trên Cloudinary.
/// </summary>
public class CloudinaryStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly string _rootFolder;

    public CloudinaryStorageService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CloudName))
        {
            throw new InvalidOperationException("Chưa cấu hình Cloudinary (section \"Cloudinary\" trong appsettings).");
        }

        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
        _rootFolder = settings.RootFolder;
    }

    public async Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = $"{_rootFolder}/{folder}",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (result.Error != null)
        {
            throw new InvalidOperationException($"Tải ảnh lên Cloudinary thất bại: {result.Error.Message}");
        }

        return new FileUploadResult(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        return result.Result == "ok";
    }
}
