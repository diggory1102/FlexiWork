using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Dịch vụ bên ngoài (VD tra tọa độ OpenStreetMap) tạm thời không phản hồi (HTTP 503).
/// </summary>
public class ExternalServiceException : AppException
{
    public ExternalServiceException(string message)
        : base(message, HttpStatusCode.ServiceUnavailable)
    {
    }
}
