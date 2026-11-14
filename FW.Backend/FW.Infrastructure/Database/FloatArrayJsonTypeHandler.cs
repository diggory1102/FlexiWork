using System.Data;
using System.Text.Json;
using Dapper;

namespace FW.Infrastructure.Database;

/// <summary>
/// Ánh xạ float[] ↔ cột JSON (VD: face_profiles.face_encoding lưu vector khuôn mặt dạng "[0.12, -0.03, ...]").
/// Đăng ký trong <see cref="DapperConfig"/>; có handler thì Dapper không còn coi mảng là danh sách tham số "IN @x".
/// </summary>
public sealed class FloatArrayJsonTypeHandler : SqlMapper.TypeHandler<float[]>
{
    public override void SetValue(IDbDataParameter parameter, float[]? value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value);
    }

    public override float[] Parse(object value) => value switch
    {
        string json => JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>(),
        byte[] utf8 => JsonSerializer.Deserialize<float[]>(utf8) ?? Array.Empty<float>(),
        _ => throw new DataException($"Không đọc được vector từ kiểu dữ liệu {value.GetType().Name}.")
    };
}
