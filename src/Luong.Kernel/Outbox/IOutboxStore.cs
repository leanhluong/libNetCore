namespace Luong.Kernel.Outbox;

/// <summary>
/// Cổng đọc/ghi bảng outbox. Bản cài đặt bằng EF Core nằm ở
/// <c>Luong.Kernel.EntityFrameworkCore</c>.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Lấy các sự kiện chưa gửi, CŨ NHẤT TRƯỚC.
    ///
    /// Lấy theo lô chứ không lấy hết: bảng outbox có thể tồn hàng trăm nghìn hàng sau
    /// một sự cố, nạp hết vào bộ nhớ là đổi một sự cố lấy một sự cố to hơn.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedOnUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi lại lý do gửi hỏng nhưng KHÔNG đánh dấu đã xử lý — để vòng sau thử lại.
    /// </summary>
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}
