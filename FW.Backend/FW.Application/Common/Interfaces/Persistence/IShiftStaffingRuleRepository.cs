using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Ngoại lệ định biên của ca theo chi nhánh (bảng shift_staffing_rules). Mỗi (chi nhánh, ca, phạm vi) một quy tắc; xóa hẳn khi bỏ ngoại lệ.
/// </summary>
public interface IShiftStaffingRuleRepository : IGenericRepository<ShiftStaffingRule>
{
    /// <summary>Quy tắc áp dụng cho các ngày trong khoảng: mọi quy tắc theo thứ trong tuần + quy tắc theo ngày cụ thể nằm trong khoảng.</summary>
    Task<IReadOnlyList<ShiftStaffingRule>> GetForRangeAsync(int branchId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>Quy tắc cần hiển thị để quản lý: mọi quy tắc theo thứ trong tuần + quy tắc theo ngày từ <paramref name="fromDate"/> trở đi.</summary>
    Task<IReadOnlyList<ShiftStaffingRule>> GetUpcomingAsync(int branchId, DateTime fromDate, CancellationToken cancellationToken = default);

    Task<ShiftStaffingRule?> GetByKeyAsync(int branchId, int shiftId, string scopeKey, CancellationToken cancellationToken = default);

    /// <summary>Thêm mới hoặc thay thế quy tắc cùng (chi nhánh, ca, phạm vi); trả về Id của bản ghi.</summary>
    Task<int> UpsertAsync(ShiftStaffingRule rule, CancellationToken cancellationToken = default);
}
