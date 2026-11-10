using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FW.API.Extensions;

/// <summary>
/// Cấu hình JSON dùng chung cho mọi phản hồi: camelCase và giữ nguyên tiếng Việt (không mã hóa thành \uXXXX).
/// </summary>
public static class ApiJson
{
    public static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = Encoder
    };
}
