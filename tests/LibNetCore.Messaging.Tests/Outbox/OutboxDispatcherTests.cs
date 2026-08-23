using LibNetCore.Core.Abstractions;
using LibNetCore.Core.Messaging;
using LibNetCore.Core.Outbox;
using LibNetCore.Messaging.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibNetCore.Messaging.Tests.Outbox;

/// <summary>Bảng outbox giả, giữ trong bộ nhớ - đủ để kiểm luật điều phối.</summary>
internal sealed class FakeOutboxStore : IOutboxStore
{
    public List<OutboxMessage> Messages { get; } = [];
    public List<Guid> Processed { get; } = [];
    public List<(Guid Id, string Error)> Failed { get; } = [];

    public Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OutboxMessage>>(
            [.. Messages.Where(m => m.ProcessedOnUtc is null).Take(batchSize)]);

    public Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedOnUtc, CancellationToken ct = default)
    {
        Processed.Add(messageId);
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        Failed.Add((messageId, error));
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPublisher : IEventPublisher
{
    public List<EventEnvelope> Published { get; } = [];

    /// <summary>Kiểu sự kiện nào nằm ở đây thì gửi sẽ hỏng — để dựng lại ca "thư độc".</summary>
    public HashSet<string> FailFor { get; } = [];

    public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        if (FailFor.Contains(envelope.Type))
        {
            throw new InvalidOperationException("Không nối được tới RabbitMQ.");
        }

        Published.Add(envelope);
        return Task.CompletedTask;
    }
}

internal sealed class FrozenClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = now;
}

public class OutboxDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeOutboxStore _store = new();
    private readonly RecordingPublisher _publisher = new();

    private OutboxDispatcher CreateDispatcher() =>
        new(_store, _publisher, new FrozenClock(Now), NullLogger<OutboxDispatcher>.Instance);

    private OutboxMessage AddMessage(string type = "Hr.EmployeeHired")
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Content = """{"FullName":"Lê Anh Lượng"}""",
            OccurredOnUtc = Now.AddMinutes(-1),
        };

        _store.Messages.Add(message);
        return message;
    }

    [Fact]
    public async Task Dispatch_PublishesEveryUnprocessedMessage()
    {
        AddMessage();
        AddMessage();

        int sent = await CreateDispatcher().DispatchAsync(batchSize: 10, CancellationToken.None);

        Assert.Equal(2, sent);
        Assert.Equal(2, _publisher.Published.Count);
    }

    [Fact]
    public async Task Dispatch_CopiesTheOutboxRowIntoTheEnvelope()
    {
        var message = AddMessage();

        await CreateDispatcher().DispatchAsync(batchSize: 10, CancellationToken.None);

        var envelope = Assert.Single(_publisher.Published);
        Assert.Equal(message.Id, envelope.EventId);
        Assert.Equal(message.Type, envelope.Type);
        Assert.Equal(message.Content, envelope.Content);
        Assert.Equal(message.OccurredOnUtc, envelope.OccurredOnUtc);
    }

    // Đánh dấu SAU khi gửi, không phải trước. Đánh dấu trước rồi gửi hỏng
    // là mất hẳn sự kiện - không ai biết mà gửi lại. Đổi lại, cách này có thể
    // gửi trùng nếu tiến trình chết giữa hai bước; bên nhận phải chịu được trùng.
    [Fact]
    public async Task Dispatch_MarksProcessedAfterPublishing()
    {
        var message = AddMessage();

        await CreateDispatcher().DispatchAsync(batchSize: 10, CancellationToken.None);

        Assert.Equal(message.Id, Assert.Single(_store.Processed));
    }

    [Fact]
    public async Task EmptyOutbox_PublishesNothing()
    {
        int sent = await CreateDispatcher().DispatchAsync(batchSize: 10, CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Empty(_publisher.Published);
    }

    // MỘT "thư độc" không được chặn cả hàng. Nếu vòng lặp dừng ở lỗi đầu tiên thì
    // một sự kiện hỏng sẽ giam toàn bộ sự kiện phía sau nó - hệ thống đứng im
    // mà nhìn vào chỉ thấy "outbox đang tồn đọng", không rõ vì sao.
    [Fact]
    public async Task OneFailingMessage_DoesNotBlockTheRest()
    {
        var poison = AddMessage("Hr.Poison");
        var healthy = AddMessage();
        _publisher.FailFor.Add("Hr.Poison");

        int sent = await CreateDispatcher().DispatchAsync(batchSize: 10, CancellationToken.None);

        Assert.Equal(1, sent);
        Assert.Equal(healthy.Id, Assert.Single(_publisher.Published).EventId);
        Assert.Equal(healthy.Id, Assert.Single(_store.Processed));
    }

    [Fact]
    public async Task FailedMessage_RecordsTheReasonAndStaysUnprocessed()
    {
        var poison = AddMessage("Hr.Poison");
        _publisher.FailFor.Add("Hr.Poison");

        await CreateDispatcher().DispatchAsync(batchSize: 10, CancellationToken.None);

        var (id, error) = Assert.Single(_store.Failed);
        Assert.Equal(poison.Id, id);
        Assert.Contains("RabbitMQ", error);
        Assert.DoesNotContain(poison.Id, _store.Processed);
    }

    [Fact]
    public async Task BatchSize_LimitsHowManyAreRead()
    {
        AddMessage();
        AddMessage();
        AddMessage();

        int sent = await CreateDispatcher().DispatchAsync(batchSize: 2, CancellationToken.None);

        Assert.Equal(2, sent);
    }
}
