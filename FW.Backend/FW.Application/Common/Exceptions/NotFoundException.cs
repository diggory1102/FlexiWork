using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi không tìm thấy tài nguyên (HTTP 404).
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message, HttpStatusCode.NotFound)
    {
    }

    public NotFoundException(string resourceName, object key)
        : base($"Không tìm thấy tài nguyên '{resourceName}' với định danh ({key}).", HttpStatusCode.NotFound)
    {
    }
}

