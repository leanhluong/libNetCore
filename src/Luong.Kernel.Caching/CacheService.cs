using System.Text.Encodings.Web;
using System.Text.Json;
using Luong.Kernel.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.Caching;

/// <summary>
/// Bản cài đặt <see cref="ICacheService"/> trên <see cref="IDistributedCache"/>.
///
/// Viết trên <c>IDistributedCache</c> chứ không viết thẳng vào StackExchange.Redis là có
/// chủ ý: Redis chỉ là một nhà cung cấp của giao diện đó. Nhờ vậy test ở đây dùng bản
/// trong bộ nhớ và kiểm được TOÀN BỘ luật (cất, lấy, xoá, cất cả giá trị rỗng) mà không
/// cần dựng Redis. Thứ test không kiểm được là hành vi qua mạng và chia sẻ giữa nhiều pod.
/// </summary>
public sealed class CacheService(IDistributedCache cache, ILogger<CacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(5);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken);

        return bytes is null ? default : Deserialize<T>(bytes, key, logger);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            // LUÔN có hạn sống. Mục không hạn sẽ nằm lại mãi cho tới khi Redis đầy bộ nhớ,
            // và lúc đó Redis bắt đầu đá ra những khoá NGẪU NHIÊN — kể cả khoá đang cần.
            AbsoluteExpirationRelativeToNow = timeToLive ?? DefaultTimeToLive,
        };

        return cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions), options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken);

        // Kiểm CÓ BYTE hay không, chứ không kiểm giá trị đọc ra có rỗng hay không.
        // Nhờ vậy giá trị rỗng cũng được coi là đã cất — xem ghi chú ở ICacheService.
        if (bytes is not null)
        {
            return Deserialize<T>(bytes, key, logger)!;
        }

        T value = await factory(cancellationToken);

        await SetAsync(key, value, timeToLive, cancellationToken);

        return value;
    }

    /// <summary>
    /// Đọc hỏng thì coi như KHÔNG có trong đệm, không ném lỗi.
    ///
    /// Nội dung cũ trong Redis có thể không còn khớp với lớp C# sau khi đổi kiểu dữ liệu.
    /// Ném lỗi lúc đó nghĩa là toàn bộ hệ thống chết cho tới khi có người vào xoá Redis
    /// bằng tay. Bỏ qua thì chỉ chậm hơn một nhịp: đọc lại từ database rồi cất bản mới đè lên.
    /// </summary>
    private static T? Deserialize<T>(byte[] bytes, string key, ILogger logger)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Nội dung đệm ở khoá {Key} không đọc được, coi như chưa có.", key);
            return default;
        }
    }
}
