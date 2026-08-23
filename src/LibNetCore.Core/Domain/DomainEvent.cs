namespace LibNetCore.Core.Domain;

/// <summary>
/// Một chuyện ĐÃ xảy ra bên trong hệ thống. Tên luôn ở thì quá khứ:
/// <c>EmployeeHired</c>, <c>LeaveRequestApproved</c> — không phải <c>HireEmployee</c>
/// (cái đó là mệnh lệnh, là <c>Command</c>).
///
/// Phân biệt với "sự kiện tích hợp" (integration event) — thứ gửi sang service khác
/// qua RabbitMQ: <b>domain event ở TRONG một tiến trình</b>, xảy ra trong cùng
/// transaction nghiệp vụ, và người nhận là code cùng service.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOnUtc { get; }
}

/// <summary>Bản cài sẵn để sự kiện cụ thể chỉ cần khai báo dữ liệu của riêng nó.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Để <c>init</c> chứ không <c>private set</c> — nhờ vậy test dựng được sự kiện
    /// với mốc thời gian cố định thay vì phải chấp nhận "lúc nào cũng là bây giờ".
    /// </summary>
    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}
