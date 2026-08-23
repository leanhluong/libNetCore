using LibNetCore.Application.Messaging;
using LibNetCore.Core.Abstractions;
using LibNetCore.Core.Primitives;
using MediatR;

namespace LibNetCore.Application.Behaviors;

/// <summary>
/// Chốt transaction sau khi mệnh lệnh chạy xong — và CHỈ khi nó thành công.
///
/// Nhờ nó, handler không phải gọi <c>SaveChangesAsync</c> ở cuối mỗi hàm. Nghe như
/// tiện lợi vặt, nhưng nó bịt một lỗ thật: handler nào quên gọi thì code chạy êm ru,
/// không lỗi, không cảnh báo — chỉ là dữ liệu <b>không được ghi</b>. Kiểu lỗi này
/// thường tới tận môi trường thật mới lộ.
///
/// <b>Chỉ bọc <see cref="IBaseCommand"/>, không bọc truy vấn.</b> Truy vấn chỉ đọc thì
/// không có gì để chốt; mở transaction cho nó là giữ kết nối lâu hơn cần thiết mà chẳng
/// được gì. Ràng buộc <c>where TRequest : IBaseCommand</c> ở dưới chính là chỗ ép điều
/// đó — MediatR tự bỏ qua behavior khi kiểu không khớp ràng buộc.
///
/// <b>Thất bại thì KHÔNG chốt.</b> Handler trả về <c>Result</c> thất bại nghĩa là
/// nghiệp vụ đã từ chối; những thay đổi lỡ nằm trong bộ theo dõi phải bị bỏ đi, không
/// được lén ghi xuống.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IBaseCommand
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);

        if (response.IsSuccess)
        {
            // Đây cũng là chỗ interceptor Outbox chạy: sự kiện và dữ liệu nghiệp vụ
            // cùng đi xuống trong MỘT transaction.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
