using LibNetCore.Core.Domain;

namespace LibNetCore.Core.Tests.Domain;

file sealed record EmployeeHired(Guid EmployeeId) : DomainEvent;

file sealed class Employee(Guid id) : AggregateRoot<Guid>(id)
{
    public static Employee Hire(Guid id)
    {
        var employee = new Employee(id);
        employee.Raise(new EmployeeHired(id));
        return employee;
    }
}

public class AggregateRootTests
{
    [Fact]
    public void NewAggregate_HasNoDomainEvents()
    {
        Assert.Empty(new Employee(Guid.NewGuid()).DomainEvents);
    }

    // Gốc tổng hợp GHI LẠI chuyện đã xảy ra, chứ không tự đi gọi ai.
    // Nhờ vậy nghiệp vụ không dính gì tới RabbitMQ hay email.
    [Fact]
    public void Raise_RecordsTheDomainEvent()
    {
        var id = Guid.NewGuid();

        var employee = Employee.Hire(id);

        var raised = Assert.Single(employee.DomainEvents);
        var hired = Assert.IsType<EmployeeHired>(raised);
        Assert.Equal(id, hired.EmployeeId);
    }

    // Sau khi phát đi rồi phải dọn - không dọn thì lần lưu tiếp theo
    // sẽ phát lại đúng sự kiện cũ thêm một lần nữa.
    [Fact]
    public void ClearDomainEvents_EmptiesTheList()
    {
        var employee = Employee.Hire(Guid.NewGuid());

        employee.ClearDomainEvents();

        Assert.Empty(employee.DomainEvents);
    }

    [Fact]
    public void DomainEvent_KnowsWhenItHappened()
    {
        var employee = Employee.Hire(Guid.NewGuid());

        var raised = Assert.Single(employee.DomainEvents);
        Assert.NotEqual(default, raised.OccurredOnUtc);
        Assert.NotEqual(Guid.Empty, raised.EventId);
    }
}
