using System.Net;

namespace FW.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi người dùng vượt hạn mức sử dụng (HTTP 429), VD hết số câu hỏi Trợ lý AI trong ngày.
/// </summary>
public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message)
        : base(message, HttpStatusCode.TooManyRequests)
    {
    }
}
