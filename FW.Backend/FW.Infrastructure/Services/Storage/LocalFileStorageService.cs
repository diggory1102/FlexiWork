using FW.Application.Common.Interfaces;

namespace FW.Infrastructure.Services.Storage;

/// <summary>
/// CHỈ DÙNG KHI PHÁT TRIỂN: lưu ảnh vào thư mục cục bộ khi chưa cấu hình Cloudinary.
/// Ảnh được phục vụ qua đường dẫn /uploads/... (xem Program.cs).
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    public const string RequestPath = "/uploads";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    /// <summary>Dùng lưu trữ cục bộ khi đang phát triển và chưa cấu hình Cloudinary.</summary>
    public static bool IsEnabled(Microsoft.Extensions.Configuration.IConfiguration configuration, bool isDevelopment) =>
        isDevelopment && string.IsNullOrWhiteSpace(configuration["Cloudinary:CloudName"]);

    public static string ResolveRootPath(Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        configuration["Storage:LocalRoot"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

    /// <param name="rootPath">Thư mục vật lý lưu ảnh.</param>
    /// <param name="publicBaseUrl">Địa chỉ gốc của API, vd: http://localhost:5010</param>
    public LocalFileStorageService(string rootPath, string publicBaseUrl)
    {
        _rootPath = rootPath;
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension)) extension = ".jpg";

        // Tên file do hệ thống sinh — không dùng tên gốc để tránh ghi đè và path traversal
        var safeFolder = string.Concat(folder.Where(char.IsLetterOrDigit));
        var publicId = $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var physicalPath = Path.Combine(_rootPath, safeFolder, Path.GetFileName(publicId));

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await using (var file = File.Create(physicalPath))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return new FileUploadResult($"{_publicBaseUrl}{RequestPath}/{publicId}", publicId);
    }

    public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var physicalPath = Path.GetFullPath(Path.Combine(_rootPath, publicId));
        if (!physicalPath.StartsWith(Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase) || !File.Exists(physicalPath))
        {
            return Task.FromResult(false);
        }
        File.Delete(physicalPath);
        return Task.FromResult(true);
    }
}
