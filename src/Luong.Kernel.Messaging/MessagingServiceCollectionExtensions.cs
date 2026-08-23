using Luong.Kernel.Messaging;
using Luong.Kernel.Messaging.Inbox;
using Luong.Kernel.Messaging.Outbox;
using Luong.Kernel.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Luong.Kernel.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký toàn bộ phần gửi/nhận sự kiện.
    ///
    /// Lưu ý vòng đời — đây là chỗ rất dễ sai:
    /// <list type="bullet">
    /// <item><c>RabbitMqConnectionProvider</c> là <b>Singleton</b>: cả tiến trình dùng
    /// chung một kết nối. Để Scoped thì mỗi request mở một kết nối mới — RabbitMQ sẽ
    /// hết chỗ nhận.</item>
    /// <item><c>OutboxDispatcher</c> và <c>InboxGuard</c> là <b>Scoped</b>: chúng phụ
    /// thuộc <c>IOutboxStore</c>/<c>IInboxStore</c>, mà hai cái đó cắm vào
    /// <c>DbContext</c> — vốn là Scoped. Để Singleton là dính đúng lỗi "phụ thuộc bị
    /// giam": một <c>DbContext</c> sống mãi tới hết đời tiến trình.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options =>
            {
                // Ném ngay lúc khởi động, kèm tên đúng của thiết lập còn thiếu.
                options.Validate();
                return true;
            });

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        services.AddScoped<OutboxDispatcher>();
        services.AddSingleton(new OutboxDispatcherSettings());
        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddScoped<InboxGuard>();

        return services;
    }
}
