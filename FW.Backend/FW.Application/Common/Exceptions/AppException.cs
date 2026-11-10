using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Lớp ngoại lệ nghiệp vụ cơ sở của hệ thống FlexiWork.
/// </summary>
public abstract class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public List<string>? Errors { get; }

    protected AppException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError, List<string>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}

