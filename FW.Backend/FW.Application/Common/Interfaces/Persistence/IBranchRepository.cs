using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

/// <summary>
/// Bộ lọc danh sách chi nhánh. Search tìm trên mã, tên, địa chỉ.
/// </summary>
public class BranchFilter : PagedQuery
{
    /// <summary>Giá trị thuộc BranchStatus.</summary>
    public string? Status { get; set; }

    /// <summary>Giới hạn về đúng một chi nhánh (dùng cho phạm vi của Manager).</summary>
    public int? BranchId { get; set; }
}

public interface IBranchRepository : IGenericRepository<Branch>
{
    Task<PaginatedResult<Branch>> GetPagedAsync(BranchFilter filter, CancellationToken cancellationToken = default);

    Task<Branch?> GetByCodeAsync(string branchCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra trùng mã chi nhánh trên TẤT CẢ bản ghi (kể cả đã xóa mềm) vì cột branch_code có ràng buộc UNIQUE.
    /// </summary>
    Task<bool> ExistsByCodeAsync(string branchCode, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IBranchWifiRepository : IGenericRepository<BranchWifi>
{
    Task<IReadOnlyList<BranchWifi>> GetByBranchIdAsync(int branchId, bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra BSSID đã được khai báo cho chi nhánh (bỏ qua bản ghi đã xóa).</summary>
    Task<bool> ExistsBssidAsync(int branchId, string bssid, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Xóa mềm toàn bộ Wi-Fi của chi nhánh (khi xóa chi nhánh).</summary>
    Task<int> DeleteByBranchIdAsync(int branchId, CancellationToken cancellationToken = default);
}
