using LibNetCore.Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Outbox;

/// <summary>
/// Bản cài đặt <see cref="IOutboxStore"/> bằng EF Core.
///
/// Nhận <see cref="DbContext"/> gốc chứ không nhận context cụ thể của service —
/// nhờ vậy một lớp này dùng được cho mọi service, miễn là context đó có gọi
/// <c>AddOutbox()</c> trong <c>OnModelCreating</c>.
/// </summary>
public sealed class EfOutboxStore(DbContext context) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null)

            // Cũ nhất trước. Không giữ thứ tự thì bên nhận có thể thấy "nhân viên nghỉ
            // việc" TRƯỚC "nhân viên được tuyển" — và xử lý sai mà không có lỗi nào báo.
            .OrderBy(m => m.OccurredOnUtc)

            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedOnUtc,
        CancellationToken cancellationToken = default)
    {
        var message = await FindAsync(messageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        message.ProcessedOnUtc = processedOnUtc;
        message.Error = null;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var message = await FindAsync(messageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        // Cố ý KHÔNG gán ProcessedOnUtc: để nguyên null thì vòng sau đọc lại và thử tiếp.
        message.Error = Truncate(error, 2000);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Không tìm thấy thì im lặng bỏ qua, không ném lỗi. Hàng có thể vừa bị một tiến
    /// trình khác dọn đi — đó là chuyện bình thường khi chạy nhiều bản, không phải sự cố.
    /// </summary>
    private async Task<OutboxMessage?> FindAsync(Guid messageId, CancellationToken cancellationToken) =>
        await context.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

    /// <summary>Cột Error giới hạn 2000 ký tự — thông báo lỗi dài hơn thì cắt, đừng để lưu hỏng.</summary>
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
