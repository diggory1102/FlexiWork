using System.Text.Encodings.Web;
using System.Text.Json;

namespace FW.API.Extensions;

/// <summary>
/// Cấu hình JSON dùng chung cho mọi phản hồi: camelCase và giữ nguyên tiếng Việt (không mã hóa thành \uXXXX).
/// </summary>
public static class ApiJson
{
    // Relaxed: không mã hóa tiếng Việt và các ký tự như ' " < > (an toàn với API JSON vì client không nhúng thẳng vào HTML)
    public static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = Encoder
    };
}
