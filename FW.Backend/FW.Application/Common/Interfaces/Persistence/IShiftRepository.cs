using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Bộ lọc danh sách khung ca. Search tìm trên mã và tên ca.
/// </summary>
public class ShiftFilter : PagedQuery
{
    /// <summary>Chỉ lấy ca của chi nhánh này (kèm ca dùng chung nếu <see cref="IncludeShared"/>).</summary>
    public int? BranchId { get; set; }

    /// <summary>Khi lọc theo chi nhánh: có lấy thêm các ca dùng chung (branch_id NULL) không.</summary>
    public bool IncludeShared { get; set; } = true;
}

public interface IShiftRepository : IGenericRepository<Shift>
{
    Task<PaginatedResult<Shift>> GetPagedAsync(ShiftFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Các ca chi nhánh được phép xếp: ca dùng chung + ca riêng của chi nhánh, sắp theo giờ bắt đầu.</summary>
    Task<IReadOnlyList<Shift>> GetAvailableForBranchAsync(int branchId, CancellationToken cancellationToken = default);

    /// <summary>Lấy nhiều ca theo Id (bỏ qua ca đã xóa) — dùng khi kiểm tra chồng giờ lúc xếp lịch.</summary>
    Task<IReadOnlyList<Shift>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra trùng mã ca trên TẤT CẢ bản ghi (kể cả đã xóa mềm) vì cột shift_code có ràng buộc UNIQUE.</summary>
    Task<bool> ExistsByCodeAsync(string shiftCode, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Ca đã được xếp vào lịch nào chưa (đang dùng thì không cho sửa giờ / xóa).</summary>
    Task<bool> IsInUseAsync(int shiftId, CancellationToken cancellationToken = default);
}
