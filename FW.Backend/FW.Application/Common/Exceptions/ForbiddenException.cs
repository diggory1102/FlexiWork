using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi người dùng không đủ quyền hạn truy cập tài nguyên (HTTP 403).
/// </summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
        : base(message, HttpStatusCode.Forbidden)
    {
    }
}

