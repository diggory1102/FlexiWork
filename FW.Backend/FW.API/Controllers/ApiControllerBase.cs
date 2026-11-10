using FW.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace FW.API.Controllers;

/// <summary>
/// Base Controller dùng chung: chuẩn hóa định dạng phản hồi ApiResponse cho mọi endpoint.
/// Các controller nghiệp vụ kế thừa lớp này thay vì ControllerBase.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Success<T>(T data, string message = "Thao tác thành công.")
        => Ok(ApiResponse<T>.SuccessResult(data, message));

    protected IActionResult Success(string message = "Thao tác thành công.")
        => Ok(ApiResponse.SuccessResult(message));

    protected IActionResult Created<T>(T data, string message = "Tạo mới thành công.")
        => StatusCode(StatusCodes.Status201Created, ApiResponse<T>.SuccessResult(data, message));
}
