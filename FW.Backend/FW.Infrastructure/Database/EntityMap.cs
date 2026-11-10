using System.Reflection;
using System.Text.RegularExpressions;
using FW.Domain.Common;

namespace FW.Infrastructure.Database;

/// <summary>
/// Siêu dữ liệu ánh xạ Entity ↔ bảng theo quy ước PascalCase ↔ snake_case,
/// dùng để BaseRepository tự sinh câu lệnh SQL. Tính một lần cho mỗi kiểu Entity.
/// Tính đúng đắn của quy ước được kiểm chứng bởi EntitySchemaMappingTests.
/// </summary>
public sealed class EntityMap
{
    // Các cột do CSDL tự quản lý — không ghi từ ứng dụng khi INSERT/UPDATE
    private static readonly HashSet<string> DatabaseManagedProperties = new()
    {
        nameof(IAuditableEntity.CreatedAt),
        nameof(IAuditableEntity.UpdatedAt),
        nameof(ISoftDelete.IsDeleted)
    };

    private EntityMap(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        SelectColumns = string.Join(", ", properties.Select(p => $"{Quote(ToSnakeCase(p.Name))} AS {p.Name}"));

        var writable = properties
            .Where(p => p.Name != "Id" && !DatabaseManagedProperties.Contains(p.Name))
            .ToList();

        InsertColumns = string.Join(", ", writable.Select(p => Quote(ToSnakeCase(p.Name))));
        InsertValues = string.Join(", ", writable.Select(p => "@" + p.Name));
        UpdateSet = string.Join(", ", writable.Select(p => $"{Quote(ToSnakeCase(p.Name))} = @{p.Name}"));

        HasSoftDelete = typeof(ISoftDelete).IsAssignableFrom(type);
    }

    /// <summary>Danh sách cột cho SELECT, có alias về tên thuộc tính.</summary>
    public string SelectColumns { get; }

    public string InsertColumns { get; }
    public string InsertValues { get; }
    public string UpdateSet { get; }
    public bool HasSoftDelete { get; }

    public static EntityMap For<T>() => Cache<T>.Instance;

    public static string ToSnakeCase(string name) =>
        Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

    public static string Quote(string identifier) => $"`{identifier}`";

    private static class Cache<T>
    {
        public static readonly EntityMap Instance = new(typeof(T));
    }
}
