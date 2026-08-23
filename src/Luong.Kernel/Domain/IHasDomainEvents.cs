namespace Luong.Kernel.Domain;

/// <summary>
/// Cửa không-generic để hạ tầng lấy được danh sách sự kiện mà không cần biết
/// kiểu khoá chính của thực thể.
///
/// Cần nó vì <see cref="AggregateRoot{TId}"/> là generic: interceptor không thể hỏi
/// "cho tôi mọi thực thể là AggregateRoot" khi chưa biết <c>TId</c> là <c>Guid</c>,
/// <c>long</c> hay <c>string</c>.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
