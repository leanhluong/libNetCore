using Luong.Kernel.Caching;
using Microsoft.Extensions.Logging.Abstractions;

namespace Luong.Kernel.Caching.Tests;

/// <summary>Kho khoá giả, giữ trong bộ nhớ - đủ để kiểm luật giành khoá và nhả khoá.</summary>
internal sealed class FakeLockStore : ILockStore
{
    private readonly Dictionary<string, string> _held = [];

    public List<(string Key, string Token)> ReleaseCalls { get; } = [];

    public int AcquireAttempts { get; private set; }

    /// <summary>Bao nhiêu lần đầu cố ý cho thất bại, để dựng ca "người khác đang giữ".</summary>
    public int FailFirstAttempts { get; set; }

    public Task<bool> TryAcquireAsync(string key, string token, TimeSpan timeToLive, CancellationToken ct = default)
    {
        AcquireAttempts++;

        if (AcquireAttempts <= FailFirstAttempts || _held.ContainsKey(key))
        {
            return Task.FromResult(false);
        }

        _held[key] = token;
        return Task.FromResult(true);
    }

    public Task<bool> ReleaseAsync(string key, string token, CancellationToken ct = default)
    {
        ReleaseCalls.Add((key, token));

        // Chỉ nhả nếu ĐÚNG người đang giữ - đây là luật quan trọng nhất, xem ghi chú trong test.
        if (_held.TryGetValue(key, out string? held) && held == token)
        {
            _held.Remove(key);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public bool IsHeld(string key) => _held.ContainsKey(key);
}

public class DistributedLockTests
{
    private readonly FakeLockStore _store = new();

    private DistributedLock CreateLock() =>
        new(_store, NullLogger<DistributedLock>.Instance, retryDelay: TimeSpan.FromMilliseconds(5));

    [Fact]
    public async Task TryAcquire_ReturnsAHandleWhenTheLockIsFree()
    {
        await using var handle = await CreateLock().TryAcquireAsync(
            "hr:employee:1", TimeSpan.FromSeconds(30), TimeSpan.Zero);

        Assert.NotNull(handle);
        Assert.True(_store.IsHeld("hr:employee:1"));
    }

    [Fact]
    public async Task TryAcquire_RetriesUntilTheLockBecomesFree()
    {
        _store.FailFirstAttempts = 2;

        await using var handle = await CreateLock().TryAcquireAsync(
            "hr:employee:1", TimeSpan.FromSeconds(30), waitFor: TimeSpan.FromMilliseconds(500));

        Assert.NotNull(handle);
        Assert.True(_store.AcquireAttempts >= 3);
    }

    // Chờ mãi là hỏng: request đang chờ khoá vẫn giữ một luồng và một kết nối database.
    // Vài trăm request cùng chờ là hết luồng, và cả service đứng - không chỉ phần dùng khoá.
    [Fact]
    public async Task TryAcquire_GivesUpAfterTheWaitTime()
    {
        _store.FailFirstAttempts = int.MaxValue;

        var handle = await CreateLock().TryAcquireAsync(
            "hr:employee:1", TimeSpan.FromSeconds(30), waitFor: TimeSpan.FromMilliseconds(50));

        Assert.Null(handle);
    }

    [Fact]
    public async Task DisposingTheHandle_ReleasesTheLock()
    {
        var lockService = CreateLock();

        var handle = await lockService.TryAcquireAsync("hr:employee:1", TimeSpan.FromSeconds(30), TimeSpan.Zero);
        await handle!.DisposeAsync();

        Assert.False(_store.IsHeld("hr:employee:1"));
    }

    // LUẬT QUAN TRỌNG NHẤT. Mỗi lần giành khoá sinh một mã riêng, và lúc nhả phải
    // đưa đúng mã đó. Không có mã thì kịch bản này xảy ra:
    //   A giành khoá 30 giây → A chạy chậm quá 30 giây → khoá tự hết hạn
    //   → B giành được → A xong việc, nhả khoá → A vừa nhả MẤT khoá CỦA B
    //   → C giành được → B và C cùng chạy. Khoá coi như không tồn tại.
    [Fact]
    public async Task EachAcquisition_UsesItsOwnToken()
    {
        var lockService = CreateLock();

        var first = await lockService.TryAcquireAsync("hr:employee:1", TimeSpan.FromSeconds(30), TimeSpan.Zero);
        string firstToken = first!.Token;
        await first.DisposeAsync();

        var second = await lockService.TryAcquireAsync("hr:employee:1", TimeSpan.FromSeconds(30), TimeSpan.Zero);
        await second!.DisposeAsync();

        Assert.NotEqual(firstToken, second.Token);
        Assert.Equal(2, _store.ReleaseCalls.Count);
        Assert.Equal(firstToken, _store.ReleaseCalls[0].Token);
    }

    [Fact]
    public async Task DisposingTwice_ReleasesOnlyOnce()
    {
        var handle = await CreateLock().TryAcquireAsync("hr:employee:1", TimeSpan.FromSeconds(30), TimeSpan.Zero);

        await handle!.DisposeAsync();
        await handle.DisposeAsync();

        Assert.Single(_store.ReleaseCalls);
    }
}
