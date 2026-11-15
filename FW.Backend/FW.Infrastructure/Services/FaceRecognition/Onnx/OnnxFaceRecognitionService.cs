using System.Diagnostics;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace FW.Infrastructure.Services.FaceRecognition.Onnx;

/// <summary>
/// Nhận diện khuôn mặt chạy ngay trong backend bằng ONNX Runtime với bộ mô hình InsightFace buffalo_sc
/// (SCRFD phát hiện mặt + ArcFace MobileFaceNet trích vector 512 chiều). Không cần dịch vụ AI riêng nên phù hợp gói
/// cloud miễn phí (1 service, ít RAM). Mô hình được nạp một lần khi dùng lần đầu và dùng chung cho mọi request.
/// </summary>
public sealed class OnnxFaceRecognitionService : IFaceRecognitionService, IDisposable
{
    /// <summary>Ảnh lớn hơn được thu nhỏ trước khi xử lý (giới hạn RAM/CPU; khuôn mặt selfie vẫn đủ lớn).</summary>
    public const int MaxImageSide = 1280;

    public const string DetectorFile = "det_500m.onnx";
    public const string EncoderFile = "w600k_mbf.onnx";

    private readonly FaceRecognitionSettings _settings;
    private readonly ILogger<OnnxFaceRecognitionService> _logger;
    private readonly Lazy<(ScrfdDetector Detector, ArcFaceEncoder Encoder)> _models;

    public OnnxFaceRecognitionService(IOptions<FaceRecognitionSettings> settings, ILogger<OnnxFaceRecognitionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _models = new Lazy<(ScrfdDetector, ArcFaceEncoder)>(LoadModels, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Trích đặc trưng từ ảnh mẫu khi đăng ký: yêu cầu chỉ có một người trong ảnh (khuôn mặt thứ hai có kích thước từ một nửa
    /// mặt lớn nhất trở lên thì từ chối), tránh đăng ký nhầm mặt người đứng cạnh.
    /// </summary>
    public Task<FaceExtractResult> ExtractEmbeddingAsync(Stream image, string fileName, CancellationToken cancellationToken = default)
    {
        var analysis = Analyze(image, requireSingleFace: true);
        return Task.FromResult(analysis.Error != null
            ? new FaceExtractResult(false, analysis.Error, analysis.Face != null, analysis.BoundingBox, null)
            : new FaceExtractResult(true, "Trích xuất đặc trưng khuôn mặt thành công.", true, analysis.BoundingBox, analysis.Embedding));
    }

    public Task<FaceVerifyResult> VerifyAsync(Stream image, string fileName, IReadOnlyList<float> targetEmbedding, CancellationToken cancellationToken = default)
    {
        var threshold = _settings.MatchThreshold;
        var analysis = Analyze(image, requireSingleFace: false);
        if (analysis.Error != null)
        {
            return Task.FromResult(new FaceVerifyResult(false, analysis.Error, false, 0, threshold));
        }

        var similarity = CosineSimilarity(analysis.Embedding!, targetEmbedding);
        var matched = similarity >= threshold;
        return Task.FromResult(new FaceVerifyResult(true,
            matched ? "Khuôn mặt khớp với dữ liệu đã đăng ký." : "Khuôn mặt không khớp với dữ liệu đã đăng ký.",
            matched, Math.Round(similarity, 4), threshold));
    }

    /// <summary>Độ tương đồng cosine của hai vector (đã chuẩn hóa thì bằng tích vô hướng), trong [-1, 1].</summary>
    public static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }

    private sealed record Analysis(string? Error, DetectedFace? Face, IReadOnlyList<int>? BoundingBox, float[]? Embedding);

    private Analysis Analyze(Stream stream, bool requireSingleFace)
    {
        RgbImage image;
        float scale;
        try
        {
            (image, scale) = RgbImage.Decode(stream, MaxImageSide);
        }
        catch (InvalidDataException)
        {
            return new Analysis("Ảnh không hợp lệ hoặc định dạng không được hỗ trợ.", null, null, null);
        }

        var (detector, encoder) = _models.Value;
        var watch = Stopwatch.StartNew();

        var faces = detector.Detect(image, _settings.DetectionThreshold);
        if (faces.Count == 0)
        {
            return new Analysis("Không tìm thấy khuôn mặt trong ảnh. Hãy chụp chính diện, đủ sáng.", null, null, null);
        }

        // Khuôn mặt lớn nhất là người đang cầm máy (selfie)
        var face = faces.MaxBy(f => f.Area)!;
        var box = new[] { face.X1, face.Y1, face.X2, face.Y2 }.Select(v => (int)Math.Round(v / scale)).ToList();
        if (Math.Min(face.Width, face.Height) / scale < _settings.MinFaceSize)
        {
            return new Analysis("Khuôn mặt quá nhỏ hoặc ở quá xa. Hãy đưa điện thoại lại gần hơn.", face, box, null);
        }
        if (requireSingleFace && faces.Any(f => f != face && f.Area >= face.Area * 0.5f))
        {
            return new Analysis("Ảnh có nhiều khuôn mặt. Hãy chụp một mình, chính diện.", face, box, null);
        }

        var embedding = encoder.Encode(FaceAligner.Align(image, face.Landmarks));
        _logger.LogDebug("Nhận diện khuôn mặt: {Faces} mặt, {Elapsed} ms", faces.Count, watch.ElapsedMilliseconds);
        return new Analysis(null, face, box, embedding);
    }

    private (ScrfdDetector, ArcFaceEncoder) LoadModels()
    {
        var folder = Path.IsPathRooted(_settings.ModelsPath)
            ? _settings.ModelsPath
            : Path.Combine(AppContext.BaseDirectory, _settings.ModelsPath);
        var detectorPath = Path.Combine(folder, DetectorFile);
        var encoderPath = Path.Combine(folder, EncoderFile);
        if (!File.Exists(detectorPath) || !File.Exists(encoderPath))
        {
            throw new InvalidOperationException(
                $"Chưa có mô hình nhận diện khuôn mặt tại '{folder}' (cần {DetectorFile}, {EncoderFile}). " +
                "Chạy FW.Backend/scripts/download-face-models.ps1 hoặc build bằng Dockerfile (tự tải mô hình).");
        }

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = Math.Max(1, _settings.IntraOpThreads),
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        var watch = Stopwatch.StartNew();
        var models = (new ScrfdDetector(detectorPath, _settings.DetectionSize, options), new ArcFaceEncoder(encoderPath, options));
        _logger.LogInformation("Đã nạp mô hình nhận diện khuôn mặt buffalo_sc trong {Elapsed} ms", watch.ElapsedMilliseconds);
        return models;
    }

    public void Dispose()
    {
        if (!_models.IsValueCreated) return;
        _models.Value.Item1.Dispose();
        _models.Value.Item2.Dispose();
    }
}
