using Microsoft.AspNetCore.Http;
using Luong.Kernel.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Luong.Kernel.AspNetCore.Errors;

/// <summary>
/// Đổi <see cref="Error"/> của tầng trong thành phản hồi HTTP chuẩn RFC 7807 (Problem Details).
///
/// Vì sao dùng chuẩn có sẵn thay vì tự bịa một khuôn JSON: RFC 7807 là thứ mà thư viện
/// client, công cụ sinh code từ OpenAPI, và người mới vào dự án đều đã biết. Tự bịa khuôn
/// riêng nghĩa là ai đụng vào cũng phải đọc tài liệu của riêng bạn.
/// </summary>
public static class ErrorProblemDetailsExtensions
{
    /// <summary>
    /// Ánh xạ loại lỗi sang mã HTTP.
    ///
    /// Đây là toàn bộ lý do <see cref="ErrorType"/> tồn tại: tầng web quyết định được
    /// mã HTTP mà KHÔNG cần biết một chữ nào về nghiệp vụ. Thiếu nó thì mỗi controller
    /// phải tự đọc mã lỗi rồi đoán — và đó là lúc code lặp bắt đầu sinh sôi.
    /// </summary>
    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static ProblemDetails ToProblemDetails(this Error error)
    {
        if (error == Error.None)
        {
            throw new InvalidOperationException("Không có lỗi thì không dựng được phản hồi lỗi.");
        }

        int status = error.Type.ToStatusCode();

        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Type = TypeUriFor(status),
        };

        // Mọi mã lỗi nghiệp vụ nằm ở đây, LUÔN LÀ MỘT DANH SÁCH — kể cả khi chỉ có
        // một lỗi. Frontend nhờ vậy viết một nhánh xử lý duy nhất, không phải hỏi
        // "lần này là một hay nhiều?".
        problem.Extensions["errors"] = ToDetails(error);

        return problem;
    }

    private static IReadOnlyList<ErrorDetail> ToDetails(Error error) => error is ValidationError validation
        ? [.. validation.Errors.Select(e => new ErrorDetail(e.Code, e.Description))]
        : [new ErrorDetail(error.Code, error.Description)];

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Internal Server Error",
    };

    private static string TypeUriFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
        _ => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
    };
}
