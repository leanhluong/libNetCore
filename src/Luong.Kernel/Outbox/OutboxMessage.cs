namespace Luong.Kernel.Outbox;

/// <summary>
/// Một sự kiện đang XẾP HÀNG CHỜ được gửi đi, nằm ngay trong database nghiệp vụ.
///
/// <b>Vấn đề nó giải — "ghi hai nơi" (dual write):</b> khi tuyển một nhân viên, ta vừa
/// phải lưu vào database, vừa phải báo cho service khác qua RabbitMQ. Hai việc, hai hệ
/// thống khác nhau, không có transaction chung. Nên luôn tồn tại một khe thời gian:
///
/// <code>
///   lưu DB thành công  →  💥 tiến trình chết  →  chưa kịp gửi RabbitMQ
///   ⇒ nhân viên có trong DB nhưng KHÔNG service nào biết. Lệch vĩnh viễn.
///
///   gửi RabbitMQ trước →  💥 lưu DB hỏng
///   ⇒ cả hệ thống tưởng có nhân viên đó, mà DB thì không. Còn tệ hơn.
/// </code>
///
/// <b>Cách chữa:</b> đừng gửi gì lúc đó cả. Ghi sự kiện xuống <i>chính database này</i>,
/// trong <i>chính transaction đang lưu nhân viên</i>. Hai hàng cùng sống hoặc cùng chết —
/// không còn khe nào. Một job nền đọc bảng này rồi mới gửi đi thật.
///
/// <b>Cái giá:</b> sự kiện tới trễ vài giây (job chạy theo chu kỳ), và người nhận có thể
/// nhận trùng nếu job chết giữa chừng sau khi gửi mà chưa kịp đánh dấu. Vì vậy người
/// nhận BẮT BUỘC phải chịu được nhận trùng — đó là việc của Inbox ở phía bên kia.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Tên kiểu đầy đủ, để đọc ngược lại thành đúng sự kiện ban đầu.</summary>
    public required string Type { get; init; }

    /// <summary>Nội dung sự kiện ở dạng JSON.</summary>
    public required string Content { get; init; }

    public DateTimeOffset OccurredOnUtc { get; init; }

    /// <summary><c>null</c> nghĩa là chưa gửi. Job nền chỉ quét những hàng này.</summary>
    public DateTimeOffset? ProcessedOnUtc { get; set; }

    /// <summary>Lý do gửi hỏng ở lần thử gần nhất. Có nó mới chẩn đoán được vì sao kẹt.</summary>
    public string? Error { get; set; }
}
