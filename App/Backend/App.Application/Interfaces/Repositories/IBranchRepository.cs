using App.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace App.Application.Interfaces.Repositories
{
    public interface IBranchRepository : IGenericRepository<Branch>
    {
        Task<Branch?> GetByCodeAsync(string branchCode);
        Task<IReadOnlyList<BranchWifi>> GetActiveWifisByBranchIdAsync(int branchId);
        Task<IReadOnlyList<Branch>> GetAllActiveBranchesAsync();
    }
}
