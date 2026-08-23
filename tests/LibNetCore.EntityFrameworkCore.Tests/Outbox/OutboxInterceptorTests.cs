using System.Text.Json;
using LibNetCore.Core.Abstractions;
using LibNetCore.Core.Domain;
using LibNetCore.Core.Outbox;
using LibNetCore.EntityFrameworkCore.Conventions;
using LibNetCore.EntityFrameworkCore.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Tests.Outbox;

internal sealed record EmployeeHired(Guid EmployeeId, string FullName) : DomainEvent;

internal sealed class Employee : AggregateRoot<Guid>
{
    private Employee() { }

    private Employee(Guid id, string fullName) : base(id) => FullName = fullName;

    public string FullName { get; set; } = string.Empty;

    public static Employee Hire(string fullName)
    {
        var employee = new Employee(Guid.NewGuid(), fullName);
        employee.Raise(new EmployeeHired(employee.Id, fullName));
        return employee;
    }

    public static Employee Import(string fullName) => new(Guid.NewGuid(), fullName);
}

internal sealed class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        modelBuilder.UseSnakeCaseNames();
    }
}


public class OutboxInterceptorTests : IDisposable
{

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public OutboxInterceptorTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private HrDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new InsertOutboxMessagesInterceptor())
            .Options;

        var context = new HrDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task SavingAnAggregate_WritesItsDomainEventsToTheOutbox()
    {
        using var context = CreateContext();
        context.Employees.Add(Employee.Hire("Lê Anh Lượng"));

        await context.SaveChangesAsync();

        var message = await context.OutboxMessages.SingleAsync();
        Assert.Contains(nameof(EmployeeHired), message.Type);
        Assert.Contains("Lê Anh Lượng", message.Content);
        Assert.Null(message.ProcessedOnUtc);
    }

    [Fact]
    public async Task OutboxContent_CanBeReadBackIntoTheOriginalEvent()
    {
        using var context = CreateContext();
        var employee = Employee.Hire("Lê Anh Lượng");
        context.Employees.Add(employee);

        await context.SaveChangesAsync();

        var message = await context.OutboxMessages.SingleAsync();
        var restored = JsonSerializer.Deserialize<EmployeeHired>(message.Content)!;
        Assert.Equal(employee.Id, restored.EmployeeId);
        Assert.Equal("Lê Anh Lượng", restored.FullName);
    }

    // Không dọn thì lần lưu sau sẽ đẩy lại đúng sự kiện cũ - người nhận
    // lĩnh "nhân viên vừa được tuyển" hai lần cho cùng một người.
    [Fact]
    public async Task DomainEvents_AreClearedSoTheyAreNotSentTwice()
    {
        using var context = CreateContext();
        var employee = Employee.Hire("Lê Anh Lượng");
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        employee.FullName = "Lê Anh Lượng (sửa)";
        await context.SaveChangesAsync();

        Assert.Empty(employee.DomainEvents);
        Assert.Single(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task AggregateWithoutEvents_WritesNothingToTheOutbox()
    {
        using var context = CreateContext();
        context.Employees.Add(Employee.Import("Nguyễn Văn A"));

        await context.SaveChangesAsync();

        Assert.Empty(await context.OutboxMessages.ToListAsync());
    }

    // LÝ DO OUTBOX TỒN TẠI. Hàng nghiệp vụ và hàng outbox nằm trong CÙNG một
    // transaction: hoặc cả hai cùng có, hoặc cả hai cùng không. Không bao giờ có
    // chuyện "đã lưu nhân viên nhưng chưa kịp báo cho ai" hay ngược lại.
    [Fact]
    public async Task BusinessRowAndOutboxRow_ShareOneTransaction()
    {
        using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        context.Employees.Add(Employee.Hire("Lê Anh Lượng"));
        await context.SaveChangesAsync();

        await transaction.RollbackAsync();

        using var fresh = CreateContext();
        Assert.Empty(await fresh.Employees.ToListAsync());
        Assert.Empty(await fresh.OutboxMessages.ToListAsync());
    }
}
