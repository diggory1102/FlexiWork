namespace FW.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ lưu trữ tệp/ảnh (selfie chấm công, ảnh Face ID, ảnh minh chứng, đính kèm đơn từ).
/// </summary>
public interface IFileStorageService
{
    /// <param name="folder">Thư mục con, ví dụ: "attendance", "faces", "tasks", "requests".</param>
    /// <returns>Kết quả tải lên gồm URL công khai và mã định danh tệp.</returns>
    Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}

public record FileUploadResult(string Url, string PublicId);
