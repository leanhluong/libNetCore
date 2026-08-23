using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LibNetCore.Messaging.RabbitMq;

/// <summary>
/// Giữ MỘT kết nối RabbitMQ dùng chung cho cả tiến trình.
///
/// Vì sao không mở kết nối mỗi lần gửi: bắt tay TCP + xác thực tốn hàng chục mili giây,
/// và RabbitMQ có giới hạn số kết nối. Mở/đóng liên tục là đúng bài "socket exhaustion"
/// mà <c>HttpClient</c> cũng dính. Kết nối thì dùng chung, còn kênh (channel) mới là thứ
/// tạo theo việc — kênh nhẹ, và KHÔNG an toàn khi nhiều luồng dùng chung một kênh.
///
/// ⚠️ Lớp này không có test tự động: muốn kiểm thật thì phải có broker đang chạy.
/// Nên nó cố tình mỏng — chỉ mở kết nối, không chứa luật nghiệp vụ nào.
/// </summary>
public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnectionProvider> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RabbitMqOptions _options = options.Value;

    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        // Nhiều luồng cùng phát hiện mất kết nối sẽ cùng lao vào mở lại.
        // Cổng này để chỉ đúng một đứa mở, những đứa còn lại chờ rồi dùng chung.
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,

                // Tự nối lại khi mạng chớp. Không bật thì một lần rung mạng là
                // service ngừng gửi được sự kiện cho tới khi có người khởi động lại.
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            logger.LogInformation("Đang mở kết nối RabbitMQ tới {Host}:{Port}", _options.Host, _options.Port);

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
