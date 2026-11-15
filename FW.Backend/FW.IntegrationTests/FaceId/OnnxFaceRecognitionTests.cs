using FW.Application.Common.Settings;
using FW.Infrastructure.Services.FaceRecognition.Onnx;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace FW.IntegrationTests.FaceId;

/// <summary>
/// Nạp mô hình ONNX thật (buffalo_sc) và chạy trọn quy trình trên ảnh không có khuôn mặt / ảnh hỏng.
/// Bỏ qua nếu chưa tải mô hình (FW.Backend/scripts/download-face-models.ps1).
/// Độ chính xác so với InsightFace gốc đã đối chiếu riêng: cosine 0,9998–0,9999, lệch điểm mốc &lt; 0,1 px.
/// </summary>
public class OnnxFaceRecognitionTests
{
    private static string? FindModels()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FW.API", "models", "buffalo_sc");
            if (File.Exists(Path.Combine(candidate, OnnxFaceRecognitionService.DetectorFile))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static OnnxFaceRecognitionService Create(string models) =>
        new(Options.Create(new FaceRecognitionSettings { ModelsPath = models }), NullLogger<OnnxFaceRecognitionService>.Instance);

    private static MemoryStream PlainImage()
    {
        using var bitmap = new SKBitmap(640, 480);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(new SKColor(200, 180, 160));
        var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Jpeg, 90);
        return new MemoryStream(data.ToArray());
    }

    [SkippableFact]
    public async Task Image_without_face_is_reported_and_garbage_is_rejected()
    {
        var models = FindModels();
        Skip.If(models == null, "Chưa tải mô hình buffalo_sc (scripts/download-face-models.ps1).");
        using var service = Create(models!);

        var noFace = await service.ExtractEmbeddingAsync(PlainImage(), "plain.jpg");
        Assert.False(noFace.Success);
        Assert.False(noFace.FaceDetected);
        Assert.Contains("Không tìm thấy khuôn mặt", noFace.Message);

        var garbage = await service.VerifyAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "x.jpg", new float[512]);
        Assert.False(garbage.Success);
        Assert.Contains("không hợp lệ", garbage.Message);
        Assert.Equal(0.40, garbage.Threshold);
    }

    [Fact]
    public async Task Missing_models_give_a_clear_setup_message()
    {
        using var service = Create(Path.Combine(Path.GetTempPath(), "khong-co-mo-hinh"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExtractEmbeddingAsync(PlainImage(), "a.jpg"));
        Assert.Contains("download-face-models", error.Message);
    }
}
