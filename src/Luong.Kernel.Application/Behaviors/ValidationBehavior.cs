using FluentValidation;
using Luong.Kernel.Primitives;
using MediatR;

namespace Luong.Kernel.Application.Behaviors;

/// <summary>
/// Kiểm dữ liệu gửi lên TRƯỚC khi handler chạy.
///
/// Vì sao để ở đây thay vì kiểm trong từng handler: kiểm trong handler thì mỗi người
/// viết một kiểu — người ném exception, người trả <c>Result</c>, người dừng ở lỗi đầu
/// tiên, người gom hết. Đặt vào một chỗ thì mọi endpoint trả lỗi kiểm dữ liệu giống hệt
/// nhau, và frontend chỉ phải xử lý đúng một dạng.
///
/// Chạy HẾT mọi luật rồi mới gom lỗi, không dừng ở luật sai đầu tiên — để form 10 ô sai
/// báo cả 10 lượt thay vì bắt người dùng sửa từng ô rồi gửi lại 10 lần.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var applicable = validators.ToList();

        if (applicable.Count == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                applicable.Select(validator => validator.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        // Dữ liệu hỏng thì handler KHÔNG được chạy chút nào — kể cả một dòng.
        var validationError = new ValidationError(
            [.. failures.Select(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage))]);

        return CreateFailure(validationError);
    }

    /// <summary>
    /// Dựng một <c>Result</c> thất bại đúng kiểu <typeparamref name="TResponse"/>.
    ///
    /// Phải dùng phản chiếu vì lúc biên dịch ta chỉ biết "<typeparamref name="TResponse"/>
    /// là một <see cref="Result"/> nào đó" — có thể là <c>Result</c> trơn, có thể là
    /// <c>Result&lt;Guid&gt;</c>. Đây là cái giá của việc viết behavior dùng chung cho
    /// mọi loại request; đổi lại là không phải viết lại luật kiểm dữ liệu cho từng loại.
    /// </summary>
    private static TResponse CreateFailure(ValidationError error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        object failure = typeof(Result)
            .GetMethods()
            .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethod)
            .MakeGenericMethod(typeof(TResponse).GenericTypeArguments[0])
            .Invoke(null, [error])!;

        return (TResponse)failure;
    }
}
