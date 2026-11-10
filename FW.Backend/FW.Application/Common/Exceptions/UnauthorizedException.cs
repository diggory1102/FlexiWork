using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi người dùng chưa đăng nhập hoặc token không hợp lệ (HTTP 401).
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.")
        : base(message, HttpStatusCode.Unauthorized)
    {
    }
}

