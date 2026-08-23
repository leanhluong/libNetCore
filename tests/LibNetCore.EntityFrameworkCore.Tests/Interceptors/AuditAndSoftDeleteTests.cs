using LibNetCore.Core.Abstractions;
using LibNetCore.Core.Domain;
using LibNetCore.EntityFrameworkCore.Conventions;
using LibNetCore.EntityFrameworkCore.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Tests.Interceptors;

internal sealed class Note : Entity<Guid>, IAuditable, ISoftDeletable
{
    public Note(Guid id, string text) : base(id) => Text = text;

    private Note() { }

    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}

internal sealed class NoteDbContext(DbContextOptions<NoteDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySoftDeleteQueryFilter();
        modelBuilder.UseSnakeCaseNames();
        modelBuilder.UseSortableDateTimeOffsets();
    }
}

/// <summary>Đồng hồ đứng yên — nhờ nó test khẳng định được giá trị chính xác.</summary>
internal sealed class FrozenClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public class AuditAndSoftDeleteTests : IDisposable
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FrozenClock _clock = new(CreatedAt);

    public AuditAndSoftDeleteTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private NoteDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NoteDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntityInterceptor(_clock), new SoftDeleteInterceptor(_clock))
            .Options;

        var context = new NoteDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Adding_StampsCreatedAt()
    {
        using var context = CreateContext();
        context.Notes.Add(new Note(Guid.NewGuid(), "xin chào"));

        await context.SaveChangesAsync();

        Assert.Equal(CreatedAt, (await context.Notes.SingleAsync()).CreatedAtUtc);
    }

    [Fact]
    public async Task Adding_LeavesUpdatedAtEmpty()
    {
        using var context = CreateContext();
        context.Notes.Add(new Note(Guid.NewGuid(), "xin chào"));

        await context.SaveChangesAsync();

        Assert.Null((await context.Notes.SingleAsync()).UpdatedAtUtc);
    }

    [Fact]
    public async Task Modifying_StampsUpdatedAt_ButKeepsCreatedAt()
    {
        using var context = CreateContext();
        context.Notes.Add(new Note(Guid.NewGuid(), "xin chào"));
        await context.SaveChangesAsync();

        _clock.UtcNow = CreatedAt.AddHours(3);
        (await context.Notes.SingleAsync()).Text = "đã sửa";
        await context.SaveChangesAsync();

        var note = await context.Notes.SingleAsync();
        Assert.Equal(CreatedAt.AddHours(3), note.UpdatedAtUtc);
        Assert.Equal(CreatedAt, note.CreatedAtUtc);
    }

    // Xoá mềm: hàng vẫn nằm trong bảng, chỉ bị đánh dấu. Dữ liệu nhân sự,
    // đơn từ, chứng từ đều KHÔNG được xoá hẳn - còn phải tra cứu và đối soát về sau.
    [Fact]
    public async Task Removing_DoesNotDeleteTheRow()
    {
        using var context = CreateContext();
        var note = new Note(Guid.NewGuid(), "xin chào");
        context.Notes.Add(note);
        await context.SaveChangesAsync();

        context.Notes.Remove(note);
        await context.SaveChangesAsync();

        var survivor = await context.Notes.IgnoreQueryFilters().SingleAsync();
        Assert.True(survivor.IsDeleted);
        Assert.Equal(_clock.UtcNow, survivor.DeletedAtUtc);
    }

    // Và mọi truy vấn bình thường phải TỰ ĐỘNG không thấy nó nữa - không bắt
    // người viết query nhớ thêm "&& !IsDeleted", vì chỉ cần quên một lần là lộ dữ liệu đã xoá.
    [Fact]
    public async Task DeletedRows_DisappearFromNormalQueries()
    {
        using var context = CreateContext();
        var note = new Note(Guid.NewGuid(), "xin chào");
        context.Notes.Add(note);
        await context.SaveChangesAsync();
        context.Notes.Remove(note);
        await context.SaveChangesAsync();

        Assert.Empty(await context.Notes.ToListAsync());
        Assert.Single(await context.Notes.IgnoreQueryFilters().ToListAsync());
    }
}
