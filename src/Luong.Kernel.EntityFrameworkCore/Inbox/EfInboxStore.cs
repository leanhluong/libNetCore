using Luong.Kernel.Inbox;
using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.Inbox;

/// <summary>Bản cài đặt <see cref="IInboxStore"/> bằng EF Core.</summary>
public sealed class EfInboxStore(DbContext context) : IInboxStore
{
    public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        context.Set<InboxMessage>().AnyAsync(m => m.Id == eventId, cancellationToken);

    public async Task MarkProcessedAsync(
        Guid eventId,
        string type,
        DateTimeOffset processedOnUtc,
        CancellationToken cancellationToken = default)
    {
        // Ghi lại chuyện đã ghi rồi thì im lặng bỏ qua. Hai tiến trình cùng xử lý một
        // sự kiện là chuyện bình thường khi chạy nhiều bản — không phải sự cố để ném lỗi.
        if (await HasProcessedAsync(eventId, cancellationToken))
        {
            return;
        }

        context.Set<InboxMessage>().Add(new InboxMessage
        {
            Id = eventId,
            Type = type,
            ProcessedOnUtc = processedOnUtc,
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
