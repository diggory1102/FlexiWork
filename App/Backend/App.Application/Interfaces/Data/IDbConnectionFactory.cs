using System.Data;

namespace App.Application.Interfaces.Data
{
    /// <summary>
    /// Factory khởi tạo kết nối IDbConnection dùng chung cho Micro-ORM Dapper
    /// </summary>
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
