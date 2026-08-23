using System.Text;
using Luong.Kernel.Messaging;
using Luong.Kernel.Messaging.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Luong.Kernel.Messaging.RabbitMq;

/// <summary>
/// Lớp gốc cho consumer: nghe một hàng đợi, chống trùng bằng inbox, rồi gọi nghiệp vụ.
///
/// Lớp con chỉ cần khai ba thứ — tên hàng đợi, nghe những khoá nào, và xử lý ra sao:
///
/// <code>
/// protected override string QueueName =&gt; "sales.hr.employee-hired";
/// protected override IReadOnlyList&lt;string&gt; RoutingKeys =&gt; ["employee-hired"];
/// protected override Task HandleAsync(EventEnvelope e, IServiceProvider scope, CancellationToken ct) { ... }
/// </code>
///
/// Quy ước tên hàng đợi <c>{ai-nghe}.{ai-phát}.{sự-kiện}</c> có lý do: nhiều service cùng
/// quan tâm một sự kiện thì mỗi bên phải có hàng đợi RIÊNG. Dùng chung một hàng đợi thì
/// hai service GIÀNH nhau thông điệp — mỗi thông điệp chỉ một bên nhận được, bên kia mất.
///
/// ⚠️ Không có test tự động: cần broker thật mới kiểm được. Vì vậy lớp này chỉ làm nhiệm
/// vụ nối dây; luật chống trùng nằm ở <see cref="InboxGuard"/> và đã có test đầy đủ.
/// </summary>
public abstract class RabbitMqConsumerBase(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    private IChannel? _channel;

    protected abstract string QueueName { get; }

    protected abstract IReadOnlyList<string> RoutingKeys { get; }

    /// <summary>Số thông điệp broker được phép đẩy sang trước khi chờ xác nhận.</summary>
    protected virtual ushort PrefetchCount => 10;

    protected abstract Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider services,
        CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Không đặt QoS thì broker đẩy TẤT CẢ thông điệp sang consumer đầu tiên nối vào.
        // Chạy 3 pod thì 1 pod ôm hết, 2 pod còn lại ngồi không — mà nhìn vào tưởng đã
        // scale ngang thành công.
        await _channel.BasicQosAsync(0, PrefetchCount, global: false, stoppingToken);

        await DeclareTopologyAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: QueueName,

            // autoAck: false — bắt buộc. Bật autoAck là broker coi như xong NGAY khi
            // đẩy đi, chưa cần biết ta xử lý được hay không. Pod chết giữa chừng là
            // thông điệp bốc hơi.
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Đang nghe hàng đợi {Queue} với các khoá {Keys}", QueueName, RoutingKeys);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task DeclareTopologyAsync(CancellationToken cancellationToken)
    {
        string deadLetterExchange = $"{_options.Exchange}.dead-letter";
        string deadLetterQueue = $"{QueueName}.dead-letter";

        await _channel!.ExchangeDeclareAsync(
            _options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            deadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            deadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            deadLetterQueue, deadLetterExchange, QueueName, cancellationToken: cancellationToken);

        // Hàng đợi chính trỏ sang sàn thư chết. Thiếu chỗ này thì thông điệp xử lý
        // không nổi sẽ quay vòng vô tận: nhận → hỏng → trả lại → nhận lại… chiếm sạch
        // CPU và làm nghẽn mọi thông điệp lành phía sau.
        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = QueueName,
            },
            cancellationToken: cancellationToken);

        foreach (string routingKey in RoutingKeys)
        {
            await _channel.QueueBindAsync(
                QueueName, _options.Exchange, routingKey, cancellationToken: cancellationToken);
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var envelope = ToEnvelope(args);

        try
        {
            // Scope riêng cho từng thông điệp: DbContext là Scoped, và mỗi thông điệp
            // phải là một đơn vị công việc độc lập — thông điệp này hỏng không được
            // kéo theo thay đổi còn dang dở của thông điệp trước.
            await using var scope = scopeFactory.CreateAsyncScope();

            var inboxGuard = scope.ServiceProvider.GetRequiredService<InboxGuard>();

            await inboxGuard.ExecuteOnceAsync(
                envelope,
                ct => HandleAsync(envelope, scope.ServiceProvider, ct),
                CancellationToken.None);

            await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Xử lý sự kiện {EventId} kiểu {Type} từ hàng đợi {Queue} thất bại. Đẩy sang thư chết.",
                envelope.EventId,
                envelope.Type,
                QueueName);

            // requeue: false — KHÔNG trả lại hàng đợi. Trả lại thì nó quay vòng mãi.
            // Cho sang thư chết để có người xem, sửa, rồi đẩy lại bằng tay.
            await _channel!.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static EventEnvelope ToEnvelope(BasicDeliverEventArgs args) => new()
    {
        EventId = Guid.TryParse(args.BasicProperties.MessageId, out var id) ? id : Guid.NewGuid(),
        Type = args.BasicProperties.Type ?? args.RoutingKey,
        Content = Encoding.UTF8.GetString(args.Body.Span),
        OccurredOnUtc = args.BasicProperties.Timestamp.UnixTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(args.BasicProperties.Timestamp.UnixTime)
            : DateTimeOffset.UtcNow,
        CorrelationId = args.BasicProperties.CorrelationId,
        Source = args.BasicProperties.AppId,
    };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
