using LibNetCore.Core.Abstractions;
using LibNetCore.Core.Inbox;
using LibNetCore.Core.Messaging;
using LibNetCore.Messaging.Inbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibNetCore.Messaging.Tests.Inbox;

internal sealed class FakeInboxStore : IInboxStore
{
    public HashSet<Guid> Processed { get; } = [];

    public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken ct = default) =>
        Task.FromResult(Processed.Contains(eventId));

    public Task MarkProcessedAsync(Guid eventId, string type, DateTimeOffset processedOnUtc, CancellationToken ct = default)
    {
        Processed.Add(eventId);
        return Task.CompletedTask;
    }
}

internal sealed class Clock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = now;
}

public class InboxGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeInboxStore _store = new();

    private InboxGuard CreateGuard() =>
        new(_store, new Clock(Now), NullLogger<InboxGuard>.Instance);

    private static EventEnvelope Envelope(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Type = "Hr.EmployeeHired",
        Content = "{}",
        OccurredOnUtc = Now.AddMinutes(-1),
    };

    [Fact]
    public async Task FirstDelivery_RunsTheHandler()
    {
        int runs = 0;

        bool executed = await CreateGuard().ExecuteOnceAsync(
            Envelope(), _ => { runs++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.True(executed);
        Assert.Equal(1, runs);
    }

    // Đây là toàn bộ lý do Inbox tồn tại: cùng một sự kiện tới lần thứ hai
    // thì KHÔNG được chạy lại - nếu không, "đã thu tiền" sẽ cộng tiền hai lượt.
    [Fact]
    public async Task SecondDeliveryOfTheSameEvent_IsSkipped()
    {
        var envelope = Envelope();
        int runs = 0;
        var guard = CreateGuard();

        await guard.ExecuteOnceAsync(envelope, _ => { runs++; return Task.CompletedTask; }, CancellationToken.None);
        bool executedAgain = await guard.ExecuteOnceAsync(
            envelope, _ => { runs++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.False(executedAgain);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task DifferentEvents_BothRun()
    {
        int runs = 0;
        var guard = CreateGuard();

        await guard.ExecuteOnceAsync(Envelope(), _ => { runs++; return Task.CompletedTask; }, CancellationToken.None);
        await guard.ExecuteOnceAsync(Envelope(), _ => { runs++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(2, runs);
    }

    // Xử lý hỏng thì KHÔNG được ghi vào sổ. Ghi vào rồi thì lần gửi lại sẽ bị
    // bỏ qua - sự kiện coi như mất hẳn, mà nhìn vào tưởng đã xử lý xong.
    [Fact]
    public async Task WhenTheHandlerThrows_NothingIsRecorded()
    {
        var envelope = Envelope();
        var guard = CreateGuard();

        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.ExecuteOnceAsync(
            envelope, _ => throw new InvalidOperationException("hỏng"), CancellationToken.None));

        Assert.Empty(_store.Processed);
    }

    [Fact]
    public async Task AfterAFailure_TheEventCanBeRetried()
    {
        var envelope = Envelope();
        var guard = CreateGuard();
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.ExecuteOnceAsync(
            envelope, _ => throw new InvalidOperationException("hỏng"), CancellationToken.None));

        int runs = 0;
        bool executed = await guard.ExecuteOnceAsync(
            envelope, _ => { runs++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.True(executed);
        Assert.Equal(1, runs);
    }
}
