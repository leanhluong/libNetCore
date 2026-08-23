namespace LibNetCore.Core.Inbox;

/// <summary>
/// Sổ ghi "sự kiện này tôi xử lý rồi".
///
/// Vì sao cần: Outbox chỉ hứa được **gửi ít nhất một lần**. Nếu job nền gửi xong mà
/// chết trước khi kịp đánh dấu, vòng sau nó gửi lại — bên nhận lĩnh cùng một sự kiện
/// hai lần. Không có sổ này thì "nhân viên vừa được tuyển" tạo ra hai hồ sơ, hoặc
/// "đã thu tiền" cộng tiền hai lượt.
///
/// Ghép hai thứ lại: Outbox (gửi ít nhất một lần) + Inbox (bỏ qua cái đã xử lý)
/// = <b>hiệu quả đúng một lần</b>. Đây là cách duy nhất đạt được điều đó, vì
/// "gửi đúng một lần" giữa hai hệ thống là chuyện không làm được.
///
/// <see cref="Id"/> chính là <c>EventId</c> trên phong bì — và cũng chính là Id hàng
/// outbox bên gửi. Gửi lại bao nhiêu lần thì Id vẫn thế, nên nhận ra ngay.
/// </summary>
public sealed class InboxMessage
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public DateTimeOffset ProcessedOnUtc { get; init; }
}
