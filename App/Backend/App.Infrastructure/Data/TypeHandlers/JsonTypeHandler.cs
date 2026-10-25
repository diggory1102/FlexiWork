using Dapper;
using System;
using System.Data;
using System.Text.Json;

namespace App.Infrastructure.Data.TypeHandlers
{
    /// <summary>
    /// Type Handler cho Dapper giúp serialize / deserialize cột JSON trong MySQL sang C# object hoặc array
    /// (Đặc biệt dùng cho vector Face ID: float[])
    /// </summary>
    public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
    {
        public override void SetValue(IDbDataParameter parameter, T? value)
        {
            parameter.Value = value == null ? DBNull.Value : JsonSerializer.Serialize(value);
            parameter.DbType = DbType.String;
        }

        public override T? Parse(object value)
        {
            if (value == null || value is DBNull)
            {
                return default;
            }

            var json = value.ToString();
            return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
        }
    }
}
