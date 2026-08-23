using System.Text;
using LibNetCore.Core.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LibNetCore.Messaging.RabbitMq;

/// <summary>
/// Bản cài đặt <see cref="IEventPublisher"/> bằng RabbitMQ.
///
/// Cố ý mỏng: nó chỉ dịch một <see cref="EventEnvelope"/> thành một thông điệp AMQP.
/// Mọi luật thật sự (gửi lô nào, gửi hỏng thì làm gì, đánh dấu lúc nào) nằm ở
/// <see cref="Outbox.OutboxDispatcher"/> — nơi có test đầy đủ. Chia như vậy vì lớp này
/// không kiểm được nếu không dựng broker, nên càng ít thứ ở đây càng ít chỗ giấu lỗi.
///
/// ⚠️ Không có test tự động che lớp này.
/// </summary>
public sealed class RabbitMqEventPublisher(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

        // Kênh tạo mới cho mỗi lần gửi rồi bỏ đi. Kênh KHÔNG an toàn khi nhiều luồng
        // dùng chung, mà lớp này có thể bị gọi song song — dùng chung một kênh là lỗi
        // rất khó lần ra vì nó chỉ hiện ra khi tải lên cao.
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: ExchangeType.Topic,

            // Sàn còn sống sau khi broker khởi động lại. Không durable thì một lần
            // restart là mọi binding biến mất và sự kiện rơi vào hư không.
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        string routingKey = RoutingKey.From(envelope.Type);

        var properties = new BasicProperties
        {
            // Thông điệp được ghi xuống đĩa. Sàn durable mà thông điệp không persistent
            // thì restart vẫn mất — phải đủ CẢ HAI mới sống sót.
            Persistent = true,

            ContentType = "application/json",

            // Bên nhận đọc MessageId để chống trùng — chính là EventId trên phong bì.
            MessageId = envelope.EventId.ToString(),
            Type = envelope.Type,
            CorrelationId = envelope.CorrelationId,
            AppId = envelope.Source,
            Timestamp = new AmqpTimestamp(envelope.OccurredOnUtc.ToUnixTimeSeconds()),
        };

        await channel.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(envelope.Content),
            cancellationToken: cancellationToken);

        logger.LogDebug(
            "Đã gửi sự kiện {EventId} tới {Exchange} với khoá {RoutingKey}",
            envelope.EventId,
            _options.Exchange,
            routingKey);
    }
}
