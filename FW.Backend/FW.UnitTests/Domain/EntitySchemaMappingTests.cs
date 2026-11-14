using System.Reflection;
using System.Text.RegularExpressions;
using FW.Domain.Entities;

namespace FW.UnitTests.Domain;

/// <summary>
/// Đảm bảo Entity trong FW.Domain khớp 1-1 với cột của bảng trong flexiwork_schema.sql
/// (theo quy ước Dapper MatchNamesWithUnderscores: PascalCase ↔ snake_case).
/// Khi sửa schema hoặc Entity mà quên sửa bên còn lại, test này sẽ báo lỗi.
/// </summary>
public class EntitySchemaMappingTests
{
    private static readonly Lazy<Dictionary<string, HashSet<string>>> Schema = new(LoadSchema);

    public static TheoryData<Type, string> EntityTables => new()
    {
        { typeof(Branch), "branches" },
        { typeof(BranchWifi), "branch_wifis" },
        { typeof(Department), "departments" },
        { typeof(Position), "positions" },
        { typeof(User), "users" },
        { typeof(Role), "roles" },
        { typeof(UserRole), "user_roles" },
        { typeof(Shift), "shifts" },
        { typeof(Schedule), "schedules" },
        { typeof(FaceProfile), "face_profiles" },
    };

    [Theory]
    [MemberData(nameof(EntityTables))]
    public void Every_entity_property_maps_to_a_column(Type entityType, string table)
    {
        var columns = GetColumns(table);
        var missing = MappedProperties(entityType)
            .Select(ToSnakeCase)
            .Where(column => !columns.Contains(column))
            .ToList();

        Assert.True(missing.Count == 0,
            $"{entityType.Name} có thuộc tính không tồn tại trong bảng {table}: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(EntityTables))]
    public void Every_column_has_an_entity_property(Type entityType, string table)
    {
        var properties = MappedProperties(entityType).Select(ToSnakeCase).ToHashSet();
        var missing = GetColumns(table).Where(column => !properties.Contains(column)).ToList();

        Assert.True(missing.Count == 0,
            $"Bảng {table} có cột chưa được khai báo trong {entityType.Name}: {string.Join(", ", missing)}");
    }

    // Chỉ lấy thuộc tính có setter — thuộc tính tính toán (vd: User.IsActive) không ánh xạ xuống CSDL.
    private static IEnumerable<string> MappedProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name);

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

    private static HashSet<string> GetColumns(string table)
    {
        Assert.True(Schema.Value.TryGetValue(table, out var columns), $"Không tìm thấy bảng {table} trong schema.");
        return columns!;
    }

    private static Dictionary<string, HashSet<string>> LoadSchema()
    {
        var sql = File.ReadAllText(FindSchemaFile());
        var tables = new Dictionary<string, HashSet<string>>();
        var constraintKeywords = new HashSet<string> { "PRIMARY", "FOREIGN", "UNIQUE", "INDEX", "KEY", "CONSTRAINT" };

        foreach (Match table in Regex.Matches(sql, @"CREATE TABLE (\w+) \((.*?)\n\) ENGINE", RegexOptions.Singleline))
        {
            var columns = table.Groups[2].Value
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("--"))
                .Select(line => line.Split(' ', 2)[0])
                .Where(word => !constraintKeywords.Contains(word.ToUpperInvariant()))
                .ToHashSet();

            tables[table.Groups[1].Value] = columns;
        }

        return tables;
    }

    private static string FindSchemaFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FW.Infrastructure", "Database", "flexiwork_schema.sql");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Không tìm thấy FW.Infrastructure/Database/flexiwork_schema.sql");
    }
}
