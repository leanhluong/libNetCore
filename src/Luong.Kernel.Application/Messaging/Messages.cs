using Luong.Kernel.Primitives;
using MediatR;

namespace Luong.Kernel.Application.Messaging;

/// <summary>
/// Mệnh lệnh làm ĐỔI trạng thái hệ thống, không trả dữ liệu.
///
/// Đây chỉ là một cái tên đặt chồng lên <see cref="IRequest{TResponse}"/> của MediatR.
/// Vì sao đáng đặt thêm tên, thay vì dùng thẳng <c>IRequest</c>:
///
/// 1. <b>Đọc một dòng là biết nó làm gì.</b> <c>ICommand</c> đổi dữ liệu, <c>IQuery</c>
///    chỉ đọc. Nhìn <c>IRequest</c> thì phải mở handler ra mới biết.
/// 2. <b>Behavior lọc được theo loại.</b> Nhờ có hai tên khác nhau,
///    <c>TransactionBehavior</c> chỉ bọc <c>ICommand</c> — truy vấn chỉ đọc thì không
///    cần mở transaction, mà mở thì vừa tốn vừa giữ kết nối lâu hơn cần thiết.
/// 3. <b>Trả về luôn là <see cref="Result"/>.</b> Ép ngay ở đây thì không handler nào
///    lỡ tay ném exception cho một thất bại đã lường trước.
/// </summary>
public interface IBaseCommand;

public interface ICommand : IRequest<Result>, IBaseCommand;

/// <summary>Mệnh lệnh đổi trạng thái VÀ trả về dữ liệu (thường là id vừa tạo).</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;

/// <summary>Câu hỏi chỉ ĐỌC, không đổi gì cả.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
