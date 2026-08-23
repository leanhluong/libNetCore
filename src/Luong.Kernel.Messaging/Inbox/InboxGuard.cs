using Luong.Kernel.Abstractions;
using Luong.Kernel.Inbox;
using Luong.Kernel.Messaging;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.Messaging.Inbox;

/// <summary>
/// Bọc quanh việc xử lý một sự kiện để nó chỉ chạy đúng một lần, dù sự kiện tới mấy lần.
///
/// <code>
/// await inboxGuard.ExecuteOnceAsync(envelope, async ct =>
/// {
///     // nghiệp vụ thật ở đây — cứ viết như thể sự kiện chỉ tới một lần
/// }, ct);
/// </code>
///
/// Nhờ nó, người viết consumer KHÔNG phải tự nghĩ về chuyện nhận trùng ở từng chỗ.
/// Bắt mỗi consumer tự lo là kiểu gì cũng có chỗ quên — và chỗ quên đó thường là chỗ
/// đụng tới tiền.
/// </summary>
public sealed class InboxGuard(
    IInboxStore store,
    IDateTimeProvider dateTimeProvider,
    ILogger<InboxGuard> logger)
{
    /// <summary>Trả về <c>true</c> nếu đã thật sự chạy, <c>false</c> nếu bỏ qua vì trùng.</summary>
    public async Task<bool> ExecuteOnceAsync(
        EventEnvelope envelope,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (await store.HasProcessedAsync(envelope.EventId, cancellationToken))
        {
            logger.LogDebug(
                "Bỏ qua sự kiện {EventId} kiểu {Type} vì đã xử lý rồi.",
                envelope.EventId,
                envelope.Type);

            return false;
        }

        await handler(cancellationToken);

        // Ghi sổ SAU khi xử lý xong. Ghi trước rồi xử lý hỏng nghĩa là lần gửi lại
        // sẽ bị bỏ qua — sự kiện mất hẳn, mà nhìn vào sổ thì tưởng đã xong.
        await store.MarkProcessedAsync(
            envelope.EventId,
            envelope.Type,
            dateTimeProvider.UtcNow,
            cancellationToken);

        return true;
    }
}
