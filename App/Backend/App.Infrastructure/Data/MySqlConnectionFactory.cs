using App.Application.Interfaces.Data;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Data;

namespace App.Infrastructure.Data
{
    public class MySqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public MySqlConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new System.InvalidOperationException("Chuỗi kết nối 'DefaultConnection' chưa được cấu hình.");
        }

        public MySqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new MySqlConnection(_connectionString);
        }
    }
}
