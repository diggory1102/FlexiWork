using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi yêu cầu không hợp lệ hoặc vi phạm ràng buộc nghiệp vụ (HTTP 400).
/// </summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message, List<string>? errors = null)
        : base(message, HttpStatusCode.BadRequest, errors)
    {
    }
}

