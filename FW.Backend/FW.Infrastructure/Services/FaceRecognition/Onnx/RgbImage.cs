using SkiaSharp;

namespace FW.Infrastructure.Services.FaceRecognition.Onnx;

/// <summary>
/// Ảnh RGB 8 bit lưu liền mạch (hàng → cột → kênh). Các phép biến đổi viết tay theo đúng quy ước của OpenCV
/// (cv2.resize INTER_LINEAR, cv2.warpAffine) để kết quả khớp với InsightFace gốc.
/// </summary>
public sealed class RgbImage
{
    public RgbImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>R, G, B của từng điểm ảnh, theo hàng.</summary>
    public byte[] Pixels { get; }

    /// <summary>
    /// Giải mã ảnh (JPEG/PNG/WebP/HEIF...) bằng SkiaSharp, thu nhỏ nếu cạnh dài vượt <paramref name="maxSide"/> (giới hạn
    /// bộ nhớ và thời gian xử lý) rồi xoay theo hướng chụp EXIF (ảnh chụp điện thoại thường lưu nằm ngang).
    /// </summary>
    /// <returns>Ảnh và hệ số đã thu nhỏ (1 = giữ nguyên) để quy đổi tọa độ về ảnh gốc.</returns>
    /// <exception cref="InvalidDataException">Không phải ảnh hoặc định dạng không được hỗ trợ.</exception>
    public static (RgbImage Image, float Scale) Decode(Stream stream, int maxSide)
    {
        using var data = SKData.Create(stream) ?? throw new InvalidDataException("Không đọc được dữ liệu ảnh.");
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Định dạng ảnh không được hỗ trợ.");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var decoded = SKBitmap.Decode(codec, info) ?? throw new InvalidDataException("Ảnh bị lỗi, không giải mã được.");

        var scale = 1f;
        var bitmap = decoded;
        SKBitmap? resized = null;
        var longest = Math.Max(decoded.Width, decoded.Height);
        if (longest > maxSide)
        {
            scale = (float)maxSide / longest;
            var target = new SKImageInfo((int)Math.Round(decoded.Width * scale), (int)Math.Round(decoded.Height * scale), SKColorType.Rgba8888, SKAlphaType.Unpremul);
            resized = decoded.Resize(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                      ?? throw new InvalidDataException("Không thu nhỏ được ảnh.");
            bitmap = resized;
        }

        try
        {
            var rgba = bitmap.GetPixelSpan();
            var (w, h, rowBytes) = (bitmap.Width, bitmap.Height, bitmap.RowBytes);
            var rgb = new byte[w * h * 3];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var s = y * rowBytes + x * 4;
                    var d = (y * w + x) * 3;
                    rgb[d] = rgba[s];
                    rgb[d + 1] = rgba[s + 1];
                    rgb[d + 2] = rgba[s + 2];
                }
            }
            return (new RgbImage(w, h, rgb).Orient(codec.EncodedOrigin), scale);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    /// <summary>Xoay / lật ảnh theo hướng chụp EXIF để khuôn mặt đứng thẳng.</summary>
    public RgbImage Orient(SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return this;

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (outW, outH) = swap ? (Height, Width) : (Width, Height);
        var output = new byte[Pixels.Length];

        for (var y = 0; y < outH; y++)
        {
            for (var x = 0; x < outW; x++)
            {
                var (sx, sy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (Width - 1 - x, y),              // lật ngang
                    SKEncodedOrigin.BottomRight => (Width - 1 - x, Height - 1 - y), // xoay 180°
                    SKEncodedOrigin.BottomLeft => (x, Height - 1 - y),           // lật dọc
                    SKEncodedOrigin.LeftTop => (y, x),                           // chuyển vị
                    SKEncodedOrigin.RightTop => (y, Height - 1 - x),             // xoay 90° theo chiều kim đồng hồ
                    SKEncodedOrigin.RightBottom => (Width - 1 - y, Height - 1 - x),
                    SKEncodedOrigin.LeftBottom => (Width - 1 - y, x),            // xoay 90° ngược chiều kim đồng hồ
                    _ => (x, y)
                };
                Buffer.BlockCopy(Pixels, (sy * Width + sx) * 3, output, (y * outW + x) * 3, 3);
            }
        }

        return new RgbImage(outW, outH, output);
    }

    /// <summary>Thay đổi kích thước bằng nội suy song tuyến (giống cv2.resize INTER_LINEAR, tâm điểm ảnh ở +0,5).</summary>
    public RgbImage Resize(int newWidth, int newHeight)
    {
        var output = new byte[newWidth * newHeight * 3];
        var sx = (float)Width / newWidth;
        var sy = (float)Height / newHeight;

        for (var y = 0; y < newHeight; y++)
        {
            var fy = Math.Max((y + 0.5f) * sy - 0.5f, 0f);
            var y0 = Math.Min((int)fy, Height - 1);
            var y1 = Math.Min(y0 + 1, Height - 1);
            var wy = fy - y0;

            for (var x = 0; x < newWidth; x++)
            {
                var fx = Math.Max((x + 0.5f) * sx - 0.5f, 0f);
                var x0 = Math.Min((int)fx, Width - 1);
                var x1 = Math.Min(x0 + 1, Width - 1);
                var wx = fx - x0;

                var o = (y * newWidth + x) * 3;
                for (var c = 0; c < 3; c++)
                {
                    var top = Pixels[(y0 * Width + x0) * 3 + c] * (1 - wx) + Pixels[(y0 * Width + x1) * 3 + c] * wx;
                    var bottom = Pixels[(y1 * Width + x0) * 3 + c] * (1 - wx) + Pixels[(y1 * Width + x1) * 3 + c] * wx;
                    output[o + c] = (byte)Math.Clamp(MathF.Round(top * (1 - wy) + bottom * wy), 0, 255);
                }
            }
        }

        return new RgbImage(newWidth, newHeight, output);
    }

    /// <summary>
    /// Biến đổi affine (giống cv2.warpAffine với nội suy song tuyến, nền đen): điểm (x, y) của ảnh đích lấy màu tại
    /// <c>inverse · (x, y, 1)</c> của ảnh nguồn.
    /// </summary>
    /// <param name="inverse">Ma trận 2×3 ánh xạ tọa độ ảnh đích → ảnh nguồn.</param>
    public RgbImage WarpAffine(float[,] inverse, int outWidth, int outHeight)
    {
        var output = new byte[outWidth * outHeight * 3];
        for (var y = 0; y < outHeight; y++)
        {
            for (var x = 0; x < outWidth; x++)
            {
                var sx = inverse[0, 0] * x + inverse[0, 1] * y + inverse[0, 2];
                var sy = inverse[1, 0] * x + inverse[1, 1] * y + inverse[1, 2];
                var x0 = (int)MathF.Floor(sx);
                var y0 = (int)MathF.Floor(sy);
                var wx = sx - x0;
                var wy = sy - y0;

                var o = (y * outWidth + x) * 3;
                for (var c = 0; c < 3; c++)
                {
                    var v = Sample(x0, y0, c) * (1 - wx) * (1 - wy) + Sample(x0 + 1, y0, c) * wx * (1 - wy)
                          + Sample(x0, y0 + 1, c) * (1 - wx) * wy + Sample(x0 + 1, y0 + 1, c) * wx * wy;
                    output[o + c] = (byte)Math.Clamp(MathF.Round(v), 0, 255);
                }
            }
        }

        return new RgbImage(outWidth, outHeight, output);
    }

    private float Sample(int x, int y, int channel) =>
        x < 0 || y < 0 || x >= Width || y >= Height ? 0f : Pixels[(y * Width + x) * 3 + channel];
}
