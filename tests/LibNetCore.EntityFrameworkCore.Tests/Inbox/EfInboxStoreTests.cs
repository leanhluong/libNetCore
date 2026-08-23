using LibNetCore.Core.Inbox;
using LibNetCore.EntityFrameworkCore.Conventions;
using LibNetCore.EntityFrameworkCore.Inbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Tests.Inbox;

internal sealed class InboxDbContext(DbContextOptions<InboxDbContext> options) : DbContext(options)
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInbox();
        modelBuilder.UseSnakeCaseNames();
        modelBuilder.UseSortableDateTimeOffsets();
    }
}

public class EfInboxStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly InboxDbContext _context;
    private readonly EfInboxStore _store;

    public EfInboxStoreTests()
    {
        _connection.Open();
        _context = new InboxDbContext(
            new DbContextOptionsBuilder<InboxDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _store = new EfInboxStore(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UnknownEvent_HasNotBeenProcessed()
    {
        Assert.False(await _store.HasProcessedAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AfterMarking_TheEventIsKnown()
    {
        var eventId = Guid.NewGuid();

        await _store.MarkProcessedAsync(eventId, "Hr.EmployeeHired", Now);

        Assert.True(await _store.HasProcessedAsync(eventId));
    }

    // Hai tiến trình cùng xử lý một sự kiện là chuyện bình thường khi chạy nhiều bản.
    // Ghi lại lần thứ hai không được ném lỗi, và không được đẻ ra hàng thứ hai.
    [Fact]
    public async Task MarkingTwice_IsHarmless()
    {
        var eventId = Guid.NewGuid();

        await _store.MarkProcessedAsync(eventId, "Hr.EmployeeHired", Now);
        await _store.MarkProcessedAsync(eventId, "Hr.EmployeeHired", Now.AddMinutes(5));

        Assert.Single(await _context.InboxMessages.ToListAsync());
    }

    [Fact]
    public async Task OneEventDoesNotHideAnother()
    {
        await _store.MarkProcessedAsync(Guid.NewGuid(), "Hr.EmployeeHired", Now);

        Assert.False(await _store.HasProcessedAsync(Guid.NewGuid()));
    }
}
