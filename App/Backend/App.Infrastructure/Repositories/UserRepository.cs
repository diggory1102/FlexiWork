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
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(AppDbContext context, IDbConnectionFactory connectionFactory) 
            : base(context)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"
                SELECT u.id, u.employee_code AS EmployeeCode, u.email, u.password_hash AS PasswordHash, 
                       u.full_name AS FullName, u.phone_number AS PhoneNumber, u.avatar_url AS AvatarUrl, 
                       u.branch_id AS BranchId, u.department_id AS DepartmentId, u.position_id AS PositionId, 
                       u.status, u.is_deleted AS IsDeleted, u.created_at AS CreatedAt, u.updated_at AS UpdatedAt
                FROM users u
                WHERE u.email = @Email AND u.is_deleted = 0
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<User?> GetByEmployeeCodeAsync(string employeeCode)
        {
            const string sql = @"
                SELECT u.id, u.employee_code AS EmployeeCode, u.email, u.password_hash AS PasswordHash, 
                       u.full_name AS FullName, u.phone_number AS PhoneNumber, u.avatar_url AS AvatarUrl, 
                       u.branch_id AS BranchId, u.department_id AS DepartmentId, u.position_id AS PositionId, 
                       u.status, u.is_deleted AS IsDeleted, u.created_at AS CreatedAt, u.updated_at AS UpdatedAt
                FROM users u
                WHERE u.employee_code = @EmployeeCode AND u.is_deleted = 0
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { EmployeeCode = employeeCode });
        }

        public async Task<FaceProfile?> GetFaceProfileByUserIdAsync(int userId)
        {
            const string sql = @"
                SELECT id, user_id AS UserId, face_encoding AS FaceEncoding, 
                       sample_image_url AS SampleImageUrl, updated_at AS UpdatedAt
                FROM face_profiles
                WHERE user_id = @UserId
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<FaceProfile>(sql, new { UserId = userId });
        }

        public async Task<IReadOnlyList<string>> GetRolesByUserIdAsync(int userId)
        {
            const string sql = @"
                SELECT r.role_name
                FROM roles r
                INNER JOIN user_roles ur ON r.id = ur.role_id
                WHERE ur.user_id = @UserId;";

            using var connection = _connectionFactory.CreateConnection();
            var roles = await connection.QueryAsync<string>(sql, new { UserId = userId });
            return roles.ToList();
        }

        public async Task<bool> UpdateFaceProfileAsync(int userId, string faceEncodingJson, string sampleImageUrl)
        {
            const string sql = @"
                INSERT INTO face_profiles (user_id, face_encoding, sample_image_url, updated_at)
                VALUES (@UserId, @FaceEncoding, @SampleImageUrl, NOW())
                ON DUPLICATE KEY UPDATE 
                    face_encoding = VALUES(face_encoding),
                    sample_image_url = VALUES(sample_image_url),
                    updated_at = NOW();";

            using var connection = _connectionFactory.CreateConnection();
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                FaceEncoding = faceEncodingJson,
                SampleImageUrl = sampleImageUrl
            });

            return rowsAffected > 0;
        }
    }
}
