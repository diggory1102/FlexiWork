namespace FW.Domain.Common;

/// <summary>
/// Giao diện hỗ trợ cơ chế xóa mềm (Soft Delete) dữ liệu.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

