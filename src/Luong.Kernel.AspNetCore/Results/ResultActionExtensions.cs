using Luong.Kernel.AspNetCore.Errors;
using Luong.Kernel.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Luong.Kernel.AspNetCore.Results;

/// <summary>
/// Đưa <see cref="Result"/> của tầng trong ra thành phản hồi của Controller.
///
/// Bản song sinh của <see cref="ResultHttpExtensions"/> — cái kia dành cho Minimal API
/// (trả <c>IResult</c>), cái này dành cho Controller (trả <c>IActionResult</c>). Hai thế
/// giới đó có hai kiểu trả khác nhau, nhưng LUẬT ánh xạ mã lỗi thì dùng chung đúng một
/// chỗ: <see cref="ErrorProblemDetailsExtensions.ToProblemDetails"/>.
///
/// Nhờ vậy action trong Controller chỉ còn một dòng, và KHÔNG action nào tự quyết định
/// mã HTTP:
///
/// <code>
/// [HttpPost("login")]
/// public async Task&lt;IActionResult&gt; Login(LoginCommand command, CancellationToken ct)
///     =&gt; (await sender.Send(command, ct)).ToActionResult();
/// </code>
/// </summary>
public static class ResultActionExtensions
{
    /// <summary>
    /// Thành công không có dữ liệu → <c>204 No Content</c>.
    /// Không dùng 200 kèm thân rỗng: 204 nói thẳng "xong rồi, không có gì để đọc".
    /// </summary>
    public static IActionResult ToActionResult(this Result result) => result.IsSuccess
        ? new NoContentResult()
        : ToProblem(result.Error);

    /// <summary>Thành công có dữ liệu → <c>200 OK</c> kèm giá trị.</summary>
    public static IActionResult ToActionResult<TValue>(this Result<TValue> result) => result.IsSuccess
        ? new OkObjectResult(result.Value)
        : ToProblem(result.Error);

    /// <summary>
    /// Tạo mới thành công → <c>201 Created</c> kèm header <c>Location</c>.
    ///
    /// Thất bại thì vẫn trả đúng mã lỗi — gọi hàm này không có nghĩa là ép ra 201.
    /// </summary>
    public static IActionResult ToCreatedResult<TValue>(this Result<TValue> result, string location) =>
        result.IsSuccess
            ? new CreatedResult(location, result.Value)
            : ToProblem(result.Error);

    private static IActionResult ToProblem(Error error)
    {
        var problem = error.ToProblemDetails();

        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,

            // Bắt buộc đặt tay. ObjectResult mặc định thương lượng ra application/json,
            // trong khi RFC 7807 quy định application/problem+json — thư viện client và
            // công cụ sinh code từ OpenAPI đều rẽ nhánh theo content-type này.
            ContentTypes = { "application/problem+json" },
        };
    }
}
