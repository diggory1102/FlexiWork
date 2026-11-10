namespace FW.Application.Common.Interfaces;

/// <summary>
/// Client giao tiếp với FW.AiService để trích xuất và đối soát vector khuôn mặt.
/// </summary>
public interface IFaceRecognitionService
{
    /// <summary>Trích xuất vector đặc trưng (512 chiều) từ ảnh chân dung mẫu.</summary>
    Task<FaceExtractResult> ExtractEmbeddingAsync(Stream image, string fileName, CancellationToken cancellationToken = default);

    /// <summary>So khớp ảnh selfie với vector mẫu đã lưu trong CSDL.</summary>
    Task<FaceVerifyResult> VerifyAsync(Stream image, string fileName, IReadOnlyList<float> targetEmbedding, CancellationToken cancellationToken = default);
}

public record FaceExtractResult(bool Success, string Message, bool FaceDetected, IReadOnlyList<int>? BoundingBox, IReadOnlyList<float>? Embedding);

public record FaceVerifyResult(bool Success, string Message, bool IsMatched, double Confidence, double Threshold);
