using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Bậc xử lý vi phạm giờ giấc. Cả bộ quy tắc được lưu lại một lần (thay toàn bộ) nên không xóa mềm.
/// </summary>
public interface IViolationRuleRepository : IGenericRepository<ViolationRule>
{
    /// <summary>Xóa toàn bộ bậc cũ rồi thêm bộ bậc mới — gọi trong transaction.</summary>
    Task ReplaceAllAsync(IReadOnlyCollection<ViolationRule> rules, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tham số hệ thống dạng khóa - giá trị (bảng system_settings).
/// </summary>
public interface ISystemSettingRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Thêm mới hoặc cập nhật giá trị của khóa.</summary>
    Task SetValueAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default);
}
