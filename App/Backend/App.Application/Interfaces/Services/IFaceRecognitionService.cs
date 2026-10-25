using System.IO;
using System.Threading.Tasks;

namespace App.Application.Interfaces.Services
{
    public class FaceVerificationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsMatched { get; set; }
        public float Confidence { get; set; }
        public float Threshold { get; set; } = 0.60f;
    }

    public class FaceExtractionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool FaceDetected { get; set; }
        public float[]? Embedding { get; set; }
    }

    /// <summary>
    /// Service giao tiếp Microservice AI Face ID
    /// </summary>
    public interface IFaceRecognitionService
    {
        Task<FaceExtractionResult> ExtractEmbeddingAsync(Stream imageStream, string fileName);
        Task<FaceVerificationResult> VerifyFaceAsync(string targetEmbeddingJson, Stream checkinImageStream, string fileName);
    }
}
