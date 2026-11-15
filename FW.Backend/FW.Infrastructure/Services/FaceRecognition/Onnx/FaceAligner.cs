namespace FW.Infrastructure.Services.FaceRecognition.Onnx;

/// <summary>
/// Căn chỉnh khuôn mặt về khung chuẩn 112×112 của ArcFace từ 5 điểm mốc (2 mắt, mũi, 2 khóe miệng) —
/// tương đương insightface.utils.face_align.norm_crop.
/// </summary>
public static class FaceAligner
{
    public const int Size = 112;

    /// <summary>Vị trí chuẩn của 5 điểm mốc trong ảnh 112×112 (arcface_dst của InsightFace).</summary>
    private static readonly (float X, float Y)[] Reference =
    {
        (38.2946f, 51.6963f),
        (73.5318f, 51.5014f),
        (56.0252f, 71.7366f),
        (41.5493f, 92.3655f),
        (70.7299f, 92.2041f),
    };

    public static RgbImage Align(RgbImage image, IReadOnlyList<(float X, float Y)> landmarks)
    {
        var m = EstimateSimilarity(landmarks, Reference);
        return image.WarpAffine(Invert(m), Size, Size);
    }

    /// <summary>
    /// Phép đồng dạng (xoay + co giãn đều + tịnh tiến) khớp <paramref name="src"/> vào <paramref name="dst"/> theo bình phương
    /// tối thiểu (nghiệm đóng của Umeyama khi không có phản xạ). Trả về ma trận 2×3 ánh xạ nguồn → đích.
    /// </summary>
    public static float[,] EstimateSimilarity(IReadOnlyList<(float X, float Y)> src, IReadOnlyList<(float X, float Y)> dst)
    {
        var n = src.Count;
        double smx = 0, smy = 0, dmx = 0, dmy = 0;
        for (var i = 0; i < n; i++)
        {
            smx += src[i].X; smy += src[i].Y;
            dmx += dst[i].X; dmy += dst[i].Y;
        }
        smx /= n; smy /= n; dmx /= n; dmy /= n;

        double num1 = 0, num2 = 0, den = 0;
        for (var i = 0; i < n; i++)
        {
            var sx = src[i].X - smx;
            var sy = src[i].Y - smy;
            var dx = dst[i].X - dmx;
            var dy = dst[i].Y - dmy;
            num1 += sx * dx + sy * dy;
            num2 += sx * dy - sy * dx;
            den += sx * sx + sy * sy;
        }

        var a = num1 / den; // s·cosθ
        var b = num2 / den; // s·sinθ
        var tx = dmx - (a * smx - b * smy);
        var ty = dmy - (b * smx + a * smy);

        return new[,]
        {
            { (float)a, (float)-b, (float)tx },
            { (float)b, (float)a, (float)ty },
        };
    }

    /// <summary>Nghịch đảo ma trận affine 2×3.</summary>
    public static float[,] Invert(float[,] m)
    {
        var det = m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0];
        var a = m[1, 1] / det;
        var b = -m[0, 1] / det;
        var c = -m[1, 0] / det;
        var d = m[0, 0] / det;
        return new[,]
        {
            { a, b, -(a * m[0, 2] + b * m[1, 2]) },
            { c, d, -(c * m[0, 2] + d * m[1, 2]) },
        };
    }
}
