using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FW.Infrastructure.Services.FaceRecognition.Onnx;

/// <summary>Một khuôn mặt phát hiện được: khung (x1, y1, x2, y2), độ tin cậy và 5 điểm mốc.</summary>
public sealed record DetectedFace(float X1, float Y1, float X2, float Y2, float Score, IReadOnlyList<(float X, float Y)> Landmarks)
{
    public float Width => X2 - X1;
    public float Height => Y2 - Y1;
    public float Area => Width * Height;
}

/// <summary>
/// Mô hình phát hiện khuôn mặt SCRFD (det_500m.onnx của bộ buffalo_sc) — tương đương insightface.model_zoo.SCRFD:
/// ảnh được thu nhỏ giữ tỉ lệ vào khung vuông, đầu ra gồm điểm, khoảng cách tới 4 cạnh và 5 điểm mốc ở 3 bước lưới 8/16/32.
/// </summary>
public sealed class ScrfdDetector : IDisposable
{
    private static readonly int[] Strides = { 8, 16, 32 };
    private const int AnchorsPerCell = 2;
    private const float NmsThreshold = 0.4f;

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string[] _outputNames;
    private readonly int _inputSize;

    public ScrfdDetector(string modelPath, int inputSize, SessionOptions options)
    {
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.First();
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        if (_outputNames.Length != 9) throw new InvalidOperationException($"Mô hình phát hiện mặt cần 9 đầu ra (có điểm mốc), nhận {_outputNames.Length}.");
        _inputSize = inputSize;
    }

    public IReadOnlyList<DetectedFace> Detect(RgbImage image, float threshold)
    {
        // 1. Thu nhỏ giữ tỉ lệ, đặt ở góc trên-trái khung vuông (phần còn lại là màu đen)
        float ratio = (float)image.Height / image.Width;
        int newW, newH;
        if (ratio > 1) { newH = _inputSize; newW = (int)(newH / ratio); }
        else { newW = _inputSize; newH = (int)(newW * ratio); }
        var scale = (float)newH / image.Height;
        var resized = image.Resize(newW, newH);

        // 2. Chuẩn hóa (x − 127.5) / 128, thứ tự kênh RGB, NCHW
        var input = new DenseTensor<float>(new[] { 1, 3, _inputSize, _inputSize });
        var buffer = input.Buffer.Span;
        var plane = _inputSize * _inputSize;
        buffer.Fill(-127.5f / 128f);
        for (var y = 0; y < newH; y++)
        {
            for (var x = 0; x < newW; x++)
            {
                var src = (y * newW + x) * 3;
                var dst = y * _inputSize + x;
                buffer[dst] = (resized.Pixels[src] - 127.5f) / 128f;
                buffer[plane + dst] = (resized.Pixels[src + 1] - 127.5f) / 128f;
                buffer[2 * plane + dst] = (resized.Pixels[src + 2] - 127.5f) / 128f;
            }
        }

        // 3. Giải mã đầu ra theo từng bước lưới
        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) }, _outputNames);
        var outputs = results.Select(r => r.AsTensor<float>()).ToArray();
        var faces = new List<DetectedFace>();

        for (var level = 0; level < Strides.Length; level++)
        {
            var stride = Strides[level];
            var scores = outputs[level];
            var boxes = outputs[level + 3];
            var kps = outputs[level + 6];
            var cells = _inputSize / stride;
            var count = cells * cells * AnchorsPerCell;

            for (var i = 0; i < count; i++)
            {
                var score = Get(scores, i, 0);
                if (score < threshold) continue;

                var cell = i / AnchorsPerCell;
                var cx = (float)(cell % cells) * stride;
                var cy = (float)(cell / cells) * stride;

                var x1 = (cx - Get(boxes, i, 0) * stride) / scale;
                var y1 = (cy - Get(boxes, i, 1) * stride) / scale;
                var x2 = (cx + Get(boxes, i, 2) * stride) / scale;
                var y2 = (cy + Get(boxes, i, 3) * stride) / scale;

                var landmarks = new (float X, float Y)[5];
                for (var k = 0; k < 5; k++)
                {
                    landmarks[k] = ((cx + Get(kps, i, 2 * k) * stride) / scale, (cy + Get(kps, i, 2 * k + 1) * stride) / scale);
                }

                faces.Add(new DetectedFace(x1, y1, x2, y2, score, landmarks));
            }
        }

        return NonMaximumSuppression(faces);
    }

    /// <summary>Đầu ra có thể ở dạng [N, k] hoặc [1, N, k] tùy bản xuất của mô hình.</summary>
    private static float Get(Tensor<float> tensor, int row, int col) =>
        tensor.Rank == 3 ? tensor[0, row, col] : tensor[row, col];

    private static List<DetectedFace> NonMaximumSuppression(List<DetectedFace> faces)
    {
        var kept = new List<DetectedFace>();
        foreach (var face in faces.OrderByDescending(f => f.Score))
        {
            if (kept.All(k => IoU(k, face) <= NmsThreshold)) kept.Add(face);
        }
        return kept;
    }

    private static float IoU(DetectedFace a, DetectedFace b)
    {
        var w = Math.Max(0, Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1) + 1);
        var h = Math.Max(0, Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1) + 1);
        var inter = w * h;
        var union = (a.Width + 1) * (a.Height + 1) + (b.Width + 1) * (b.Height + 1) - inter;
        return inter / union;
    }

    public void Dispose() => _session.Dispose();
}

/// <summary>
/// Mô hình trích xuất đặc trưng ArcFace (w600k_mbf.onnx — MobileFaceNet huấn luyện trên WebFace600K) — tương đương
/// insightface.model_zoo.ArcFaceONNX: ảnh mặt đã căn chỉnh 112×112, chuẩn hóa (x − 127.5) / 127.5, ra vector 512 chiều.
/// </summary>
public sealed class ArcFaceEncoder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    public ArcFaceEncoder(string modelPath, SessionOptions options)
    {
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.First();
    }

    /// <returns>Vector đặc trưng đã chuẩn hóa L2 (độ dài 1).</returns>
    public float[] Encode(RgbImage alignedFace)
    {
        var size = FaceAligner.Size;
        var input = new DenseTensor<float>(new[] { 1, 3, size, size });
        var buffer = input.Buffer.Span;
        var plane = size * size;
        for (var i = 0; i < plane; i++)
        {
            buffer[i] = (alignedFace.Pixels[i * 3] - 127.5f) / 127.5f;
            buffer[plane + i] = (alignedFace.Pixels[i * 3 + 1] - 127.5f) / 127.5f;
            buffer[2 * plane + i] = (alignedFace.Pixels[i * 3 + 2] - 127.5f) / 127.5f;
        }

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) });
        var embedding = results.First().AsEnumerable<float>().ToArray();

        var norm = MathF.Sqrt(embedding.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < embedding.Length; i++) embedding[i] /= norm;
        }
        return embedding;
    }

    public void Dispose() => _session.Dispose();
}
