namespace FW.Application.Features.FaceId;

/// <summary>Độ tương đồng giữa hai vector đặc trưng khuôn mặt (dùng chung cho chấm công và chặn đăng ký trùng).</summary>
public static class FaceSimilarity
{
    /// <summary>Độ tương đồng cosine của hai vector (đã chuẩn hóa thì bằng tích vô hướng), trong [-1, 1]; khác số chiều hoặc rỗng → 0.</summary>
    public static double Cosine(IReadOnlyList<float> a, IReadOnlyList<float> b)
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
}
