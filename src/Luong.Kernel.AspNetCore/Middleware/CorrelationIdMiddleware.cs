using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.AspNetCore.Middleware;

/// <summary>
/// Gắn cho mỗi request một mã lần vết duy nhất.
///
/// Vì sao cần: một thao tác của người dùng có thể đi qua gateway → service A → hàng đợi →
/// service B. Khi nó hỏng, log nằm rải ở bốn chỗ và không có gì nối chúng lại. Mã lần vết
/// chính là sợi dây đó — có nó thì lọc log một lần ra đủ cả hành trình.
///
/// Luật quan trọng: nếu request ĐÃ mang sẵn mã thì GIỮ NGUYÊN, không sinh mã mới.
/// Mỗi service tự sinh mã riêng thì một hành trình sẽ có bốn mã khác nhau — mất sạch
/// tác dụng, mà lại tưởng là mình có.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        string correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming.ToString())
                ? incoming.ToString()
                : Guid.NewGuid().ToString("N");

        // Chỗ mọi tầng khác đọc lại được mà không cần tiêm thêm phụ thuộc gì.
        context.TraceIdentifier = correlationId;

        // Gắn vào lúc bắt đầu ghi phản hồi — kể cả khi phản hồi đó là một lỗi.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Mọi dòng log sinh ra bên trong request này tự khắc mang theo mã.
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>Đăng ký SỚM NHẤT có thể — mọi middleware sau nó đều cần mã lần vết.</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
