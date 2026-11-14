using FW.Domain.Common;

namespace FW.Domain.Entities;

/// <summary>
/// Dữ liệu khuôn mặt mẫu của một nhân viên (bảng face_profiles, mỗi nhân viên tối đa một bản ghi).
/// Vector đặc trưng do FW.AiService (InsightFace) trích xuất từ ảnh mẫu; khi chấm công, ảnh selfie được
/// so khớp với vector này. Đây là dữ liệu sinh trắc học — chỉ dùng cho chấm công, không trả về client.
/// </summary>
public class FaceProfile : BaseEntity, IAuditableEntity
{
    /// <summary>Số chiều vector của mô hình InsightFace buffalo_l (ArcFace).</summary>
    public const int EmbeddingSize = 512;

    public const string DefaultModelName = "buffalo_l";

    public int UserId { get; set; }

    /// <summary>Vector đặc trưng khuôn mặt (<see cref="EmbeddingSize"/> số thực), lưu dạng mảng JSON.</summary>
    public float[] FaceEncoding { get; set; } = Array.Empty<float>();

    /// <summary>Ảnh mẫu trên kho lưu trữ (Cloudinary).</summary>
    public string SampleImageUrl { get; set; } = string.Empty;

    /// <summary>Mã ảnh trên kho lưu trữ — dùng để xóa ảnh cũ khi đăng ký lại.</summary>
    public string? SampleImagePublicId { get; set; }

    /// <summary>Mô hình đã trích xuất vector; vector của hai mô hình khác nhau không so khớp được với nhau.</summary>
    public string ModelName { get; set; } = DefaultModelName;

    /// <summary>Do CSDL tự gán — không ghi từ ứng dụng.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
