using App.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace App.Application.Interfaces.Repositories
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByEmployeeCodeAsync(string employeeCode);
        Task<FaceProfile?> GetFaceProfileByUserIdAsync(int userId);
        Task<IReadOnlyList<string>> GetRolesByUserIdAsync(int userId);
        Task<bool> UpdateFaceProfileAsync(int userId, string faceEncodingJson, string sampleImageUrl);
    }
}
