namespace LibNetCore.Core.Messaging;

/// <summary>
/// Cổng gửi sự kiện ra khỏi service.
///
/// Nằm ở <c>Core</c> chứ không nằm ở package RabbitMQ vì đây là một CỔNG, không phải
/// một bản cài đặt: phần điều phối outbox chỉ cần biết "có thứ gì đó gửi hộ tôi",
/// không cần biết đó là RabbitMQ, Kafka, hay một hàng đợi giả trong test.
///
/// Đây chính là chỗ mũi tên phụ thuộc bị bẻ ngược: tầng trong định nghĩa cổng,
/// tầng ngoài đi theo. Không có nó thì phần điều phối phải tham chiếu RabbitMQ,
/// và mọi test của nó sẽ cần một broker thật đang chạy.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
}
