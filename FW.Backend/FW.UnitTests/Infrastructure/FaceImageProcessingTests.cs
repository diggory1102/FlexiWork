using FW.Infrastructure.Services.FaceRecognition.Onnx;
using SkiaSharp;

namespace FW.UnitTests.Infrastructure;

/// <summary>
/// Phần xử lý ảnh viết tay của bộ nhận diện ONNX: ước lượng phép đồng dạng (căn chỉnh khuôn mặt), xoay ảnh theo EXIF,
/// độ tương đồng cosine. Độ khớp với InsightFace gốc đã đối chiếu riêng (cosine 0,9998–0,9999 trên 7 khuôn mặt).
/// </summary>
public class FaceImageProcessingTests
{
    private static (float X, float Y) Apply(float[,] m, (float X, float Y) p) =>
        (m[0, 0] * p.X + m[0, 1] * p.Y + m[0, 2], m[1, 0] * p.X + m[1, 1] * p.Y + m[1, 2]);

    [Fact]
    public void Similarity_estimate_recovers_a_known_rotation_scale_and_translation()
    {
        // Phép biến đổi thật: xoay 30°, phóng 1,5 lần, dịch (10, -4)
        var angle = Math.PI / 6;
        var (a, b) = ((float)(1.5 * Math.Cos(angle)), (float)(1.5 * Math.Sin(angle)));
        var truth = new[,] { { a, -b, 10f }, { b, a, -4f } };

        var src = new (float X, float Y)[] { (30, 40), (70, 42), (50, 60), (35, 80), (66, 81) };
        var dst = src.Select(p => Apply(truth, p)).ToArray();

        var estimated = FaceAligner.EstimateSimilarity(src, dst);
        for (var r = 0; r < 2; r++)
            for (var c = 0; c < 3; c++)
                Assert.Equal(truth[r, c], estimated[r, c], 3);
    }

    [Fact]
    public void Inverse_matrix_maps_points_back()
    {
        var m = new[,] { { 1.2f, -0.4f, 7f }, { 0.4f, 1.2f, -3f } };
        var inverse = FaceAligner.Invert(m);
        var back = Apply(inverse, Apply(m, (12.5f, 40f)));
        Assert.Equal(12.5f, back.X, 3);
        Assert.Equal(40f, back.Y, 3);
    }

    [Fact]
    public void Aligned_face_is_112_square()
    {
        var image = new RgbImage(200, 200, new byte[200 * 200 * 3]);
        var landmarks = new (float X, float Y)[] { (70, 90), (130, 90), (100, 120), (75, 150), (125, 150) };
        var aligned = FaceAligner.Align(image, landmarks);
        Assert.Equal(112, aligned.Width);
        Assert.Equal(112, aligned.Height);
    }

    /// <summary>Ảnh 3×2: mỗi điểm ảnh mang giá trị R = chỉ số (0..5) để theo dõi vị trí sau khi xoay.</summary>
    private static RgbImage Numbered()
    {
        var pixels = new byte[3 * 2 * 3];
        for (var i = 0; i < 6; i++) pixels[i * 3] = (byte)i;
        return new RgbImage(3, 2, pixels);
    }

    private static int[] Values(RgbImage image) => Enumerable.Range(0, image.Width * image.Height).Select(i => (int)image.Pixels[i * 3]).ToArray();

    // Ảnh gốc 3×2:  0 1 2 / 3 4 5
    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft, 3, 2, new[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData(SKEncodedOrigin.TopRight, 3, 2, new[] { 2, 1, 0, 5, 4, 3 })]       // lật ngang
    [InlineData(SKEncodedOrigin.BottomRight, 3, 2, new[] { 5, 4, 3, 2, 1, 0 })]    // xoay 180°
    [InlineData(SKEncodedOrigin.BottomLeft, 3, 2, new[] { 3, 4, 5, 0, 1, 2 })]     // lật dọc
    [InlineData(SKEncodedOrigin.RightTop, 2, 3, new[] { 3, 0, 4, 1, 5, 2 })]       // xoay 90° theo chiều kim đồng hồ
    [InlineData(SKEncodedOrigin.LeftBottom, 2, 3, new[] { 2, 5, 1, 4, 0, 3 })]     // xoay 90° ngược chiều kim đồng hồ
    [InlineData(SKEncodedOrigin.LeftTop, 2, 3, new[] { 0, 3, 1, 4, 2, 5 })]        // chuyển vị
    [InlineData(SKEncodedOrigin.RightBottom, 2, 3, new[] { 5, 2, 4, 1, 3, 0 })]
    public void Exif_orientation_puts_the_image_upright(SKEncodedOrigin origin, int width, int height, int[] expected)
    {
        var oriented = Numbered().Orient(origin);
        Assert.Equal(width, oriented.Width);
        Assert.Equal(height, oriented.Height);
        Assert.Equal(expected, Values(oriented));
    }

    [Fact]
    public void Decoding_garbage_reports_invalid_image()
    {
        Assert.Throws<InvalidDataException>(() => RgbImage.Decode(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }), 1280));
    }

    [Fact]
    public void Large_images_are_scaled_down_before_processing()
    {
        using var bitmap = new SKBitmap(2560, 1440);
        using var png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);

        var (image, scale) = RgbImage.Decode(png.AsStream(), 1280);
        Assert.Equal(1280, image.Width);
        Assert.Equal(720, image.Height);
        Assert.Equal(0.5f, scale, 3);
    }

    [Theory]
    [InlineData(new[] { 0.6f, 0.8f }, new[] { 0.6f, 0.8f }, 1.0)]
    [InlineData(new[] { 1f, 0f }, new[] { 0f, 1f }, 0.0)]
    [InlineData(new[] { 1f, 0f }, new[] { -2f, 0f }, -1.0)]
    [InlineData(new[] { 1f, 0f }, new[] { 1f }, 0.0)] // khác số chiều
    public void Cosine_similarity(float[] a, float[] b, double expected)
    {
        Assert.Equal(expected, OnnxFaceRecognitionService.CosineSimilarity(a, b), 6);
    }
}
