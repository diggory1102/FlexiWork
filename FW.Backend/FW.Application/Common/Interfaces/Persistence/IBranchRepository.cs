using FW.Domain.Entities;

namespace FW.Application.Common.Interfaces.Persistence;

public interface IBranchRepository : IGenericRepository<Branch>
{
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
}
