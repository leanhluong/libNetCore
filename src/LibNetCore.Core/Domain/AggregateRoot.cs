namespace LibNetCore.Core.Domain;

/// <summary>
/// Gốc tổng hợp: thực thể đứng đầu một cụm dữ liệu phải luôn đúng cùng nhau,
/// và là CỬA DUY NHẤT để bên ngoài đụng vào cụm đó.
///
/// Ví dụ: <c>Đơn nghỉ phép</c> là gốc, các <c>dòng phê duyệt</c> nằm bên trong nó.
/// Không ai được sửa thẳng một dòng phê duyệt — phải đi qua đơn, vì chỉ đơn mới biết
/// luật "duyệt xong rồi thì không sửa được nữa".
///
/// Nó GHI LẠI chuyện đã xảy ra chứ KHÔNG tự đi gọi ai. Nghiệp vụ nhờ vậy không dính
/// một dòng nào về RabbitMQ, email hay SignalR — hạ tầng đọc danh sách sự kiện này
/// sau khi lưu thành công rồi mới phát đi. Đây chính là chỗ Outbox sẽ cắm vào.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>Dành cho EF Core.</summary>
    protected AggregateRoot()
    {
    }

    /// <summary>Chỉ đọc — bên ngoài không được tự thêm bớt sự kiện của người khác.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Gọi sau khi đã phát sự kiện đi. Quên dọn thì lần lưu tiếp theo sẽ phát lại
    /// đúng những sự kiện cũ thêm một lần nữa.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
