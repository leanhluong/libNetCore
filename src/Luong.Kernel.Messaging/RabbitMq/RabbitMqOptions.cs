namespace Luong.Kernel.Messaging.RabbitMq;

/// <summary>Cấu hình kết nối RabbitMQ, đọc từ <c>appsettings</c> hoặc biến môi trường.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Sàn giao dịch kiểu <c>topic</c> mà service này phát ra, theo quy ước
    /// <c>{tên-service}.events</c> — ví dụ <c>hr.events</c>.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>Mỗi vòng điều phối đọc tối đa bao nhiêu hàng outbox.</summary>
    public int OutboxBatchSize { get; set; } = 20;

    /// <summary>
    /// Kiểm ngay lúc khởi động.
    ///
    /// Thà chết ngay với thông báo nói rõ THIẾU CÁI GÌ, còn hơn khởi động thành công rồi
    /// im lặng không gửi được sự kiện nào — kiểu hỏng đó có khi vài ngày sau mới có người
    /// nhận ra, lúc dữ liệu giữa các service đã lệch nhau rồi.
    /// </summary>
    public void Validate()
    {
        Require(Host, nameof(Host));
        Require(UserName, nameof(UserName));
        Require(Password, nameof(Password));
        Require(Exchange, nameof(Exchange));

        if (OutboxBatchSize < 1)
        {
            throw new InvalidOperationException($"RabbitMq: {nameof(OutboxBatchSize)} phải lớn hơn 0.");
        }
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"RabbitMq: thiếu cấu hình bắt buộc '{name}'.");
        }
    }
}
