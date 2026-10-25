using App.Application.Interfaces.Data;
using App.Application.Interfaces.Repositories;
using App.Domain.Entities;
using App.Infrastructure.Data;
using Dapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace App.Infrastructure.Repositories
{
    public class BranchRepository : GenericRepository<Branch>, IBranchRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public BranchRepository(AppDbContext context, IDbConnectionFactory connectionFactory) 
            : base(context)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Branch?> GetByCodeAsync(string branchCode)
        {
            const string sql = @"
                SELECT id, branch_code AS BranchCode, name, address, phone, email, 
                       latitude, longitude, radius, status, is_deleted AS IsDeleted, 
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM branches 
                WHERE branch_code = @BranchCode AND is_deleted = 0 
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<Branch>(sql, new { BranchCode = branchCode });
        }

        public async Task<IReadOnlyList<BranchWifi>> GetActiveWifisByBranchIdAsync(int branchId)
        {
            const string sql = @"
                SELECT id, branch_id AS BranchId, ssid, bssid, 
                       is_active AS IsActive, is_deleted AS IsDeleted, 
                       created_at AS CreatedAt
                FROM branch_wifis 
                WHERE branch_id = @BranchId AND is_active = 1 AND is_deleted = 0;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<BranchWifi>(sql, new { BranchId = branchId });
            return result.ToList();
        }

        public async Task<IReadOnlyList<Branch>> GetAllActiveBranchesAsync()
        {
            const string sql = @"
                SELECT id, branch_code AS BranchCode, name, address, phone, email, 
                       latitude, longitude, radius, status, is_deleted AS IsDeleted, 
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM branches 
                WHERE status = 'active' AND is_deleted = 0;";

            using var connection = _connectionFactory.CreateConnection();
            var result = await connection.QueryAsync<Branch>(sql);
            return result.ToList();
        }
    }
}
