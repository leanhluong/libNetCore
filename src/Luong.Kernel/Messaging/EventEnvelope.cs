namespace Luong.Kernel.Messaging;

/// <summary>
/// Phong bì bọc quanh một sự kiện khi nó rời khỏi service.
///
/// Vì sao không gửi thẳng nội dung sự kiện: bên nhận cần những thứ KHÔNG thuộc về
/// nghiệp vụ nhưng bắt buộc phải có để xử lý cho đúng:
///
/// <list type="bullet">
/// <item><see cref="EventId"/> — để nhận diện đã xử lý cái này chưa. Đây là nền của
/// việc chống trùng: outbox bảo đảm "gửi ít nhất một lần", nghĩa là có lúc gửi hai lần.</item>
/// <item><see cref="Type"/> — để biết đọc nội dung ra thành kiểu gì.</item>
/// <item><see cref="CorrelationId"/> — sợi dây nối log giữa các service. Không có nó,
/// một hành trình đi qua bốn service là bốn đống log rời rạc.</item>
/// <item><see cref="Source"/> — service nào phát ra. Cần khi đi tìm nguồn của một
/// sự kiện lạ.</item>
/// </list>
///
/// Nội dung để nguyên dạng JSON chứ không đọc ngược thành đối tượng: bên gửi KHÔNG cần
/// nạp được kiểu của sự kiện, và như vậy tránh được chuyện service này phải tham chiếu
/// assembly của service kia chỉ để chuyển tiếp một chuỗi.
/// </summary>
public sealed record EventEnvelope
{
    public required Guid EventId { get; init; }

    public required string Type { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset OccurredOnUtc { get; init; }

    public string? CorrelationId { get; init; }

    public string? Source { get; init; }
}
