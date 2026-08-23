using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Luong.Kernel.Realtime;

public static class RealtimeServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký SignalR, cắm backplane Redis khi chạy nhiều bản.
    ///
    /// <b>Backplane là gì và vì sao bắt buộc phải có khi chạy ≥2 pod:</b>
    /// mỗi pod chỉ giữ danh sách kết nối CỦA RIÊNG NÓ. Người dùng A nối vào pod 1,
    /// người dùng B nối vào pod 2. Pod 1 gọi <c>Clients.All.SendAsync(...)</c> — B
    /// không nhận được gì cả, vì pod 1 không biết B tồn tại.
    ///
    /// Backplane bắc cầu qua Redis Pub/Sub: pod 1 phát lên Redis, pod 2 nghe được rồi
    /// đẩy tiếp cho B. Không có nó, hệ thống realtime <b>vỡ ngẫu nhiên</b> khi scale
    /// ngang — lúc được lúc không, tuỳ hai người vô tình rơi vào cùng pod hay khác pod.
    /// Đây là kiểu lỗi tệ nhất để đi tìm, vì máy dev một pod thì không bao giờ tái hiện.
    /// </summary>
    /// <param name="channelPrefix">
    /// Tiền tố kênh Pub/Sub, thường là tên service. BẮT BUỘC đặt khi nhiều service dùng
    /// chung một Redis: thiếu nó thì tin nhắn của service này lọt sang service kia.
    /// </param>
    public static ISignalRServerBuilder AddRealtime(
        this IServiceCollection services,
        string? redisConnectionString,
        string channelPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelPrefix);

        services.AddSingleton<IUserIdProvider, ClaimsUserIdProvider>();

        var builder = services.AddSignalR(options =>
        {
            // Không đẩy chi tiết exception về client: thông báo lỗi thật hay chứa
            // tên bảng, tên cột, đường dẫn file.
            options.EnableDetailedErrors = false;

            // Client im lặng quá lâu thì coi như đã mất. Mặc định 30 giây; đặt rõ ra
            // đây để người đọc thấy con số, thay vì phải tra tài liệu.
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        });

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            // Chạy một pod (máy dev) thì không cần backplane. Nhưng phải BIẾT là mình
            // đang chạy không có nó — chứ không phải vô tình thiếu.
            return builder;
        }

        builder.AddStackExchangeRedis(redisConnectionString, options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal(channelPrefix);
        });

        return builder;
    }
}
