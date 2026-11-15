namespace FW.Application.Common.Settings;

/// <summary>
/// Cấu hình nhận diện khuôn mặt (section "FaceRecognition").
/// </summary>
public class FaceRecognitionSettings
{
    public const string SectionName = "FaceRecognition";

    /// <summary>Chạy mô hình ngay trong backend (ONNX Runtime) — mặc định khi triển khai.</summary>
    public const string OnnxEngine = "Onnx";

    /// <summary>Gọi sang microservice FW.AiService (FastAPI + InsightFace) qua HTTP.</summary>
    public const string AiServiceEngine = "AiService";

    /// <summary><see cref="OnnxEngine"/> hoặc <see cref="AiServiceEngine"/>.</summary>
    public string Engine { get; set; } = OnnxEngine;

    /// <summary>Thư mục chứa det_500m.onnx và w600k_mbf.onnx (bộ buffalo_sc); đường dẫn tương đối tính từ thư mục ứng dụng.</summary>
    public string ModelsPath { get; set; } = "models/buffalo_sc";

    /// <summary>Kích thước ảnh đưa vào mô hình phát hiện mặt (vuông). 640 chính xác hơn, 320 nhanh hơn ~4 lần.</summary>
    public int DetectionSize { get; set; } = 640;

    /// <summary>Ngưỡng tin cậy tối thiểu của một khuôn mặt được phát hiện.</summary>
    public float DetectionThreshold { get; set; } = 0.5f;

    /// <summary>Độ tương đồng cosine tối thiểu để coi là cùng một người (hiệu chỉnh ở thực nghiệm Chương 4).</summary>
    public double MatchThreshold { get; set; } = 0.40;

    /// <summary>Cạnh ngắn tối thiểu (px, trên ảnh gốc) của khuôn mặt — nhỏ hơn thì yêu cầu chụp gần hơn.</summary>
    public int MinFaceSize { get; set; } = 64;

    /// <summary>Số luồng CPU cho mỗi lần chạy mô hình (Render free chỉ có 0,1 CPU → 1 luồng).</summary>
    public int IntraOpThreads { get; set; } = 1;
}
