using LibNetCore.Core.Outbox;
using LibNetCore.EntityFrameworkCore.Conventions;
using LibNetCore.EntityFrameworkCore.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Tests.Outbox;

internal sealed class OutboxOnlyDbContext(DbContextOptions<OutboxOnlyDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        modelBuilder.UseSnakeCaseNames();
        modelBuilder.UseSortableDateTimeOffsets();
    }
}

public class EfOutboxStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly OutboxOnlyDbContext _context;
    private readonly EfOutboxStore _store;

    public EfOutboxStoreTests()
    {
        _connection.Open();
        _context = new OutboxOnlyDbContext(
            new DbContextOptionsBuilder<OutboxOnlyDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _store = new EfOutboxStore(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<OutboxMessage> AddAsync(DateTimeOffset occurredOn, DateTimeOffset? processedOn = null)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "Hr.EmployeeHired",
            Content = "{}",
            OccurredOnUtc = occurredOn,
            ProcessedOnUtc = processedOn,
        };

        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    [Fact]
    public async Task GetUnprocessed_SkipsMessagesAlreadySent()
    {
        await AddAsync(Now.AddMinutes(-5), processedOn: Now);
        var waiting = await AddAsync(Now.AddMinutes(-1));

        var result = await _store.GetUnprocessedAsync(batchSize: 10);

        Assert.Equal(waiting.Id, Assert.Single(result).Id);
    }

    // Cũ nhất trước. Không giữ thứ tự thì bên nhận có thể thấy "nhân viên nghỉ việc"
    // TRƯỚC "nhân viên được tuyển" - và xử lý sai mà không hề báo lỗi.
    [Fact]
    public async Task GetUnprocessed_ReturnsOldestFirst()
    {
        var newer = await AddAsync(Now.AddMinutes(-1));
        var older = await AddAsync(Now.AddMinutes(-10));

        var result = await _store.GetUnprocessedAsync(batchSize: 10);

        Assert.Equal([older.Id, newer.Id], result.Select(m => m.Id));
    }

    [Fact]
    public async Task GetUnprocessed_RespectsBatchSize()
    {
        await AddAsync(Now.AddMinutes(-3));
        await AddAsync(Now.AddMinutes(-2));
        await AddAsync(Now.AddMinutes(-1));

        var result = await _store.GetUnprocessedAsync(batchSize: 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task MarkProcessed_TakesTheMessageOutOfTheQueue()
    {
        var message = await AddAsync(Now.AddMinutes(-1));

        await _store.MarkProcessedAsync(message.Id, Now);

        Assert.Empty(await _store.GetUnprocessedAsync(batchSize: 10));
        Assert.Equal(Now, (await _context.OutboxMessages.SingleAsync()).ProcessedOnUtc);
    }

    // Ghi lý do hỏng nhưng VẪN để trong hàng chờ - vòng sau phải thử lại.
    [Fact]
    public async Task MarkFailed_RecordsTheReasonButKeepsItQueued()
    {
        var message = await AddAsync(Now.AddMinutes(-1));

        await _store.MarkFailedAsync(message.Id, "Không nối được tới RabbitMQ.");

        var stored = await _context.OutboxMessages.SingleAsync();
        Assert.Equal("Không nối được tới RabbitMQ.", stored.Error);
        Assert.Null(stored.ProcessedOnUtc);
        Assert.Single(await _store.GetUnprocessedAsync(batchSize: 10));
    }

    [Fact]
    public async Task MarkingAMissingMessage_DoesNotThrow()
    {
        await _store.MarkProcessedAsync(Guid.NewGuid(), Now);
        await _store.MarkFailedAsync(Guid.NewGuid(), "gì đó");
    }
}
