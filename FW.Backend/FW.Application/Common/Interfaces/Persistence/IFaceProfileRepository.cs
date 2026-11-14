using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Dữ liệu khuôn mặt mẫu — mỗi nhân viên tối đa một bản ghi (user_id UNIQUE), xóa hẳn khi hủy đăng ký.
/// </summary>
public interface IFaceProfileRepository : IGenericRepository<FaceProfile>
{
    Task<FaceProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Thêm mới hoặc thay thế dữ liệu khuôn mặt của nhân viên (đăng ký lại).</summary>
    /// <returns>Id của bản ghi.</returns>
    Task<int> UpsertAsync(FaceProfile profile, CancellationToken cancellationToken = default);

    Task<bool> DeleteByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Trong các nhân viên cho trước, những ai đã đăng ký khuôn mặt (hiển thị trạng thái Face ID trên danh sách).</summary>
    Task<IReadOnlySet<int>> GetRegisteredUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default);
}
