namespace LibNetCore.Core.Inbox;

/// <summary>Cổng đọc/ghi sổ inbox. Bản cài đặt EF nằm ở <c>LibNetCore.EntityFrameworkCore</c>.</summary>
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        Guid eventId,
        string type,
        DateTimeOffset processedOnUtc,
        CancellationToken cancellationToken = default);
}
