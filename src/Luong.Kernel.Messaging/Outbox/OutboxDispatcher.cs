using Luong.Kernel.Abstractions;
using Luong.Kernel.Messaging;
using Luong.Kernel.Outbox;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.Messaging.Outbox;

/// <summary>
/// Nửa còn lại của Outbox: đọc những sự kiện đang nằm chờ trong database rồi gửi đi thật.
///
/// <code>
///   [nghiệp vụ]  Raise(EmployeeHired)
///        ↓       (không biết RabbitMQ là gì)
///   [interceptor] ghi hàng outbox — CÙNG transaction với dữ liệu nghiệp vụ
///        ↓
///   [lớp này]     đọc hàng chưa gửi → gửi → đánh dấu đã gửi     ← đang ở đây
/// </code>
///
/// Không phụ thuộc Hangfire hay bất kỳ bộ hẹn giờ nào — nó chỉ biết làm một vòng.
/// Ai gọi nó theo chu kỳ là chuyện của <c>Luong.Kernel.Jobs</c>. Tách như vậy thì test
/// được toàn bộ luật điều phối mà không cần dựng bộ hẹn giờ nào cả.
/// </summary>
public sealed class OutboxDispatcher(
    IOutboxStore store,
    IEventPublisher publisher,
    IDateTimeProvider dateTimeProvider,
    ILogger<OutboxDispatcher> logger)
{
    /// <summary>Chạy một vòng. Trả về số sự kiện gửi thành công.</summary>
    public async Task<int> DispatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var messages = await store.GetUnprocessedAsync(batchSize, cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        int sent = 0;

        foreach (var message in messages)
        {
            // try/catch nằm TRONG vòng lặp, không nằm ngoài. Đặt ngoài thì một sự kiện
            // hỏng sẽ giam toàn bộ sự kiện phía sau nó — hệ thống đứng im mà nhìn vào
            // chỉ thấy "outbox đang tồn đọng", không rõ vì đâu.
            try
            {
                await publisher.PublishAsync(ToEnvelope(message), cancellationToken);

                // Đánh dấu SAU khi gửi. Đánh dấu trước rồi gửi hỏng là mất hẳn sự kiện,
                // không ai biết mà gửi lại. Đổi lại, cách này có thể gửi TRÙNG nếu tiến
                // trình chết giữa hai bước — nên bên nhận bắt buộc phải chịu được trùng.
                await store.MarkProcessedAsync(message.Id, dateTimeProvider.UtcNow, cancellationToken);

                sent++;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Gửi sự kiện outbox {MessageId} kiểu {Type} thất bại. Sẽ thử lại ở vòng sau.",
                    message.Id,
                    message.Type);

                // Ghi lý do nhưng KHÔNG đánh dấu đã xử lý → vòng sau đọc lại và thử tiếp.
                await store.MarkFailedAsync(message.Id, exception.Message, cancellationToken);
            }
        }

        return sent;
    }

    private static EventEnvelope ToEnvelope(OutboxMessage message) => new()
    {
        // Dùng chính Id của hàng outbox làm Id sự kiện. Nhờ vậy dù gửi lại bao nhiêu
        // lần thì bên nhận vẫn thấy CÙNG một Id — đó là thứ để họ nhận ra đã xử lý rồi.
        EventId = message.Id,
        Type = message.Type,
        Content = message.Content,
        OccurredOnUtc = message.OccurredOnUtc,
    };
}
