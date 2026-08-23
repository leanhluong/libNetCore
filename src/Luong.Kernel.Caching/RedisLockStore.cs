using Luong.Kernel.Caching;
using StackExchange.Redis;

namespace Luong.Kernel.Caching;

/// <summary>
/// Bản Redis của <see cref="ILockStore"/>.
///
/// ⚠️ Không có test tự động: cần Redis thật mới kiểm được. Vì vậy nó chỉ có hai lệnh —
/// toàn bộ luật (thử lại, bỏ cuộc, nhả đúng mã) nằm ở <see cref="DistributedLock"/>,
/// nơi đã được test đầy đủ.
/// </summary>
public sealed class RedisLockStore(IConnectionMultiplexer redis) : ILockStore
{
    /// <summary>
    /// Nhả khoá phải là MỘT thao tác không thể chen ngang: so mã rồi mới xoá.
    ///
    /// Viết bằng C# hai bước (đọc mã → nếu khớp thì xoá) là sai, vì giữa hai bước đó khoá
    /// có thể hết hạn và người khác giành được — thế là xoá mất khoá của họ. Redis chạy
    /// đoạn Lua này liền một mạch, không ai chen vào giữa được.
    /// </summary>
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public Task<bool> TryAcquireAsync(
        string key,
        string token,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        // When.NotExists = chỉ đặt nếu khoá CHƯA tồn tại. Đây chính là phép "giành khoá",
        // và Redis bảo đảm nó là một thao tác duy nhất — hai pod cùng gọi thì chỉ một
        // pod nhận được true.
        return redis.GetDatabase().StringSetAsync(key, token, timeToLive, When.NotExists);
    }

    public async Task<bool> ReleaseAsync(string key, string token, CancellationToken cancellationToken = default)
    {
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            ReleaseScript,
            [key],
            [token]);

        return (int)result == 1;
    }
}
