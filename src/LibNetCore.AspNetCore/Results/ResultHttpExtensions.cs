using LibNetCore.AspNetCore.Errors;
using LibNetCore.Core.Primitives;
using Microsoft.AspNetCore.Http;

namespace LibNetCore.AspNetCore.Results;

/// <summary>
/// Mảnh nối cuối cùng: đưa <see cref="Result"/> của tầng trong ra thành phản hồi HTTP.
///
/// Nhờ nó mà endpoint chỉ còn đúng một dòng, và KHÔNG endpoint nào phải tự quyết định
/// mã HTTP nữa:
///
/// <code>
/// app.MapPost("/employees", async (CreateEmployee cmd, ISender sender) =>
///     (await sender.Send(cmd)).ToHttpResult());
/// </code>
///
/// Nếu thiếu mảnh này, mỗi endpoint sẽ tự viết <c>if (result.IsFailure) return ...</c>
/// theo kiểu riêng — và chỉ cần vài chục endpoint là hệ thống có vài chục cách trả lỗi
/// khác nhau, frontend phải chiều từng cái một.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// Thành công mà không có dữ liệu trả về → <c>204 No Content</c>.
    /// Không dùng <c>200</c> kèm thân rỗng: 204 nói thẳng "xong rồi, không có gì để đọc".
    /// </summary>
    public static IResult ToHttpResult(this Result result) => result.IsSuccess
        ? TypedResults.NoContent()
        : ToProblem(result.Error);

    /// <summary>Thành công có dữ liệu → <c>200 OK</c> kèm giá trị.</summary>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result) => result.IsSuccess
        ? TypedResults.Ok(result.Value)
        : ToProblem(result.Error);

    private static IResult ToProblem(Error error) => TypedResults.Problem(error.ToProblemDetails());
}
