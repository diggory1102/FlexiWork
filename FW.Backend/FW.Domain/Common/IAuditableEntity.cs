namespace FW.Domain.Common;

/// <summary>
/// Giao diện chuẩn hóa thời gian khởi tạo và cập nhật của bản ghi dữ liệu.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

