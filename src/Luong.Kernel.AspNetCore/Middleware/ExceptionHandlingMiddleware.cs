using Luong.Kernel.AspNetCore.Errors;
using Luong.Kernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.AspNetCore.Middleware;

/// <summary>
/// Lưới an toàn cuối cùng: mọi exception không ai bắt đều dừng ở đây.
///
/// Hai việc nó làm, và việc thứ hai mới là lý do nó tồn tại:
///
/// 1. GHI LOG đầy đủ — có stack trace, có mã lần vết. Đây là thứ dành cho người sửa lỗi.
///
/// 2. TRẢ RA NGOÀI một phản hồi KHÔNG có gì bên trong. Thông báo exception thật thường
///    chứa chuỗi kết nối, tên máy chủ, đường dẫn file, đôi khi cả mật khẩu. Đẩy nguyên
///    ra ngoài là tặng không bản đồ hệ thống cho kẻ tấn công. Người dùng chỉ nhận được
///    một mã lần vết để đọc cho bộ phận hỗ trợ — từ mã đó ta tìm ra đúng dòng log.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Request {Method} {Path} hỏng ngoài dự kiến. CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            // Đã trót gửi byte đầu tiên đi rồi thì không sửa được phản hồi nữa.
            // Cố ghi đè lúc này chỉ tạo ra phản hồi vỡ đôi, khó chẩn đoán hơn hẳn.
            if (context.Response.HasStarted)
            {
                throw;
            }

            var problem = Error
                .Failure("Server.Unexpected", "Đã xảy ra lỗi ngoài dự kiến. Vui lòng thử lại sau.")
                .ToProblemDetails();

            problem.Extensions["correlationId"] = context.TraceIdentifier;

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;

            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                context.RequestAborted);
        }
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    /// <summary>Đăng ký ngay sau <c>UseCorrelationId</c> và trước mọi thứ khác.</summary>
    public static IApplicationBuilder UseProblemDetailsExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
