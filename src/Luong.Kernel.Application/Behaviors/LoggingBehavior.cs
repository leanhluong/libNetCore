using Luong.Kernel.Primitives;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.Application.Behaviors;

/// <summary>
/// Ghi log mỗi use case: bắt đầu, kết thúc, thất bại vì lý do gì.
///
/// Đăng ký NGOÀI CÙNG, trước mọi behavior khác. Nằm ngoài thì nó thấy được cả những
/// request bị <c>ValidationBehavior</c> chặn và cả những mệnh lệnh bị huỷ transaction —
/// tức là thấy toàn bộ câu chuyện. Đặt trong cùng thì đúng những ca hỏng lại là những
/// ca không có log, và đó chính là lúc cần log nhất.
///
/// Log mã lỗi (<c>Employee.EmailTaken</c>) chứ không log câu chữ: mã ổn định nên đếm và
/// gom nhóm được — "hôm nay có 340 lần EmailTaken" là một con số dùng được, còn đếm theo
/// câu chữ thì hỏng ngay lần đầu ai đó sửa chính tả.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string name = typeof(TRequest).Name;

        logger.LogInformation("Bắt đầu {UseCase}", name);

        TResponse response = await next(cancellationToken);

        if (response.IsSuccess)
        {
            logger.LogInformation("Hoàn tất {UseCase}", name);
        }
        else
        {
            logger.LogWarning("{UseCase} thất bại: {ErrorCode}", name, response.Error.Code);
        }

        return response;
    }
}
