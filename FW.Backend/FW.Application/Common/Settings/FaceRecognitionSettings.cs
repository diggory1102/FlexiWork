namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình nhận diện khuôn mặt (section "FaceRecognition").
/// </summary>
public class FaceRecognitionSettings
{
    public const string SectionName = "FaceRecognition";

    /// <summary>Thư mục chứa det_500m.onnx và w600k_mbf.onnx (bộ buffalo_sc); đường dẫn tương đối tính từ thư mục ứng dụng.</summary>
    public string ModelsPath { get; set; } = "models/buffalo_sc";

    /// <summary>Kích thước ảnh đưa vào mô hình phát hiện mặt (vuông). 640 chính xác hơn, 320 nhanh hơn ~4 lần.</summary>
    public int DetectionSize { get; set; } = 640;

    /// <summary>Ngưỡng tin cậy tối thiểu của một khuôn mặt được phát hiện.</summary>
    public float DetectionThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Độ tương đồng cosine tối thiểu để coi là cùng một người. Mặc định 0,35 theo Task 6.2 (đo trên LFW, 127 người, ~1,15 triệu cặp khác người, sáu điều kiện
    /// ảnh — công cụ <c>FW.Backend/tools/FaceThresholdEval</c>): FAR (người lạ lọt qua) chỉ 0,002–0,007% ở mọi điều kiện và gần như phẳng từ 0,35 đến 0,50
    /// nhưng tăng gấp ~4 lần khi xuống 0,30, nên 0,35 là điểm "khuỷu" — còn FRR (người thật bị từ chối) chỉ bằng một nửa so với 0,40 khi ảnh kém
    /// (nhòe 8% so với 17%). Cần đo lại bằng ảnh chấm công thật ở Task 6.5.
    /// </summary>
    public double MatchThreshold { get; set; } = 0.35;

    /// <summary>
    /// Chặn đăng ký trùng: nếu khuôn mặt mới giống khuôn mặt đã đăng ký của một nhân viên đang làm khác từ ngưỡng này trở lên thì từ chối
    /// (một người dùng chung khuôn mặt cho hai tài khoản để chấm công hộ). Mặc định 0,35 = <see cref="MatchThreshold"/>: đo trên LFW (Task 6.2)
    /// cho thấy so với 0,50 cũ, ngưỡng này bắt được 97% cặp cùng người (thay vì 84%) với tỉ lệ chặn nhầm ~0,4% mỗi lần đăng ký ở công ty 80 người
    /// (thay vì 0,14%). Thực tế dùng <see cref="EffectiveDuplicateThreshold"/>.
    /// </summary>
    public double DuplicateThreshold { get; set; } = 0.35;

    /// <summary>
    /// Ngưỡng chặn trùng thực sự dùng: không bao giờ cao hơn <see cref="MatchThreshold"/>. Nếu một khuôn mặt mới đủ giống khuôn mặt đã đăng ký của
    /// tài khoản khác để QUA được bước chấm công của tài khoản đó thì dù thế nào cũng phải coi là trùng — nên chỉnh <see cref="MatchThreshold"/>
    /// xuống thì ngưỡng chặn trùng tự hạ theo, không để lọt khoảng giữa hai ngưỡng.
    /// </summary>
    public double EffectiveDuplicateThreshold => Math.Min(DuplicateThreshold, MatchThreshold);

    /// <summary>Cạnh ngắn tối thiểu (px, trên ảnh gốc) của khuôn mặt — nhỏ hơn thì yêu cầu chụp gần hơn.</summary>
    public int MinFaceSize { get; set; } = 64;

    /// <summary>Số luồng CPU cho mỗi lần chạy mô hình (Render free chỉ có 0,1 CPU → 1 luồng).</summary>
    public int IntraOpThreads { get; set; } = 1;
}
