using LibNetCore.Core.Caching;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LibNetCore.Caching;

public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký bộ đệm Redis và khoá dùng chung.
    /// </summary>
    /// <param name="instanceName">
    /// Tiền tố gắn trước mọi khoá, thường là tên service. BẮT BUỘC đặt khi nhiều service
    /// dùng chung một Redis: thiếu nó thì khoá <c>employee:1</c> của service này đè lên
    /// <c>employee:1</c> của service kia — mà không có lỗi nào báo, chỉ có dữ liệu sai.
    /// </param>
    public static IServiceCollection AddRedisCaching(
        this IServiceCollection services,
        string connectionString,
        string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = $"{instanceName}:";
        });

        // Một kết nối dùng chung cho cả tiến trình — cùng lý do với RabbitMQ:
        // bắt tay lại mỗi lần dùng là tự tạo ra nút thắt.
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<ILockStore, RedisLockStore>();
        services.AddSingleton<IDistributedLock>(provider =>
            new DistributedLock(
                provider.GetRequiredService<ILockStore>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DistributedLock>>()));

        return services;
    }
}
