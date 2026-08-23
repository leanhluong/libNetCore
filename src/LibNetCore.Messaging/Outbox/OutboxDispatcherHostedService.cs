using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibNetCore.Messaging.Outbox;

/// <summary>
/// Chạy <see cref="OutboxDispatcher"/> theo chu kỳ, suốt vòng đời tiến trình.
///
/// <b>Vì sao KHÔNG dùng Hangfire cho việc này</b> — dù dự án vẫn có Hangfire cho việc khác:
/// Hangfire hẹn giờ theo cron, mà cron nhỏ nhất là <b>một phút</b>. Nghĩa là sự kiện có
/// thể nằm chờ tới 60 giây trước khi được gửi. Với "nhân viên vừa được tuyển" thì không
/// sao, nhưng với thông báo hay tin nhắn thì một phút là quá lâu.
///
/// <see cref="PeriodicTimer"/> chạy được chu kỳ vài giây, không cần thêm bảng, không cần
/// thêm hạ tầng. Ranh giới nên nhớ:
/// <list type="bullet">
/// <item><b>Việc của hạ tầng, chạy liên tục, chu kỳ tính bằng giây</b> → BackgroundService.</item>
/// <item><b>Việc nghiệp vụ có lịch</b> ("8h sáng gửi nhắc chấm công", "mùng 1 chốt báo cáo"),
/// cần xem lại lịch sử chạy, cần bấm chạy lại bằng tay → Hangfire.</item>
/// </list>
///
/// ⚠️ Vòng lặp hẹn giờ không có test tự động. Toàn bộ luật thật nằm ở
/// <see cref="OutboxDispatcher"/> và đã được test đầy đủ — lớp này chỉ gọi nó lặp lại.
/// </summary>
public sealed class OutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherHostedService> logger,
    OutboxDispatcherSettings settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Bắt đầu điều phối outbox mỗi {Seconds} giây, mỗi vòng tối đa {BatchSize} sự kiện.",
            settings.Interval.TotalSeconds,
            settings.BatchSize);

        using var timer = new PeriodicTimer(settings.Interval);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                // Mỗi vòng một scope riêng: OutboxDispatcher phụ thuộc DbContext, mà
                // DbContext là Scoped. Giữ một cái sống suốt đời tiến trình thì bộ nhớ
                // phình theo số thực thể đã đọc, và dữ liệu đọc ra ngày càng cũ.
                await using var scope = scopeFactory.CreateAsyncScope();

                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

                int sent = await dispatcher.DispatchAsync(settings.BatchSize, stoppingToken);

                if (sent > 0)
                {
                    logger.LogInformation("Đã gửi {Count} sự kiện từ outbox.", sent);
                }
            }
            catch (Exception exception)
            {
                // Nuốt lỗi ở đây là CỐ Ý. Để lọt ra ngoài thì BackgroundService chết hẳn
                // và không bao giờ chạy lại — outbox tồn đọng vô thời hạn mà chẳng ai
                // hay, vì ứng dụng vẫn phục vụ request bình thường.
                logger.LogError(exception, "Một vòng điều phối outbox hỏng. Vòng sau vẫn chạy tiếp.");
            }
        }

        logger.LogInformation("Đã dừng điều phối outbox.");
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Ứng dụng đang tắt — đây là kết thúc bình thường, không phải sự cố.
            return false;
        }
    }
}

/// <summary>Thiết lập cho vòng lặp điều phối outbox.</summary>
public sealed class OutboxDispatcherSettings
{
    /// <summary>Mặc định 10 giây: đủ nhanh để người dùng không thấy trễ, đủ thưa để không quần database.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

    public int BatchSize { get; set; } = 20;
}
