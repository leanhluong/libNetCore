using Luong.Kernel.Domain;
using Luong.Kernel.EntityFrameworkCore.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.Tests.Conventions;

file sealed class Department : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
}

file sealed class Employee : Entity<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
}

file sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>().HasIndex(e => e.FullName);
        modelBuilder.UseSnakeCaseNames();
    }
}

public class SnakeCaseNamingTests
{
    private static DbContext CreateContext() =>
        new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite("DataSource=:memory:").Options);

    // Tên bảng lấy từ tên thuộc tính DbSet (đã ở dạng số nhiều) rồi đổi sang snake_case.
    [Fact]
    public void TableNames_BecomeSnakeCase()
    {
        using var context = CreateContext();

        Assert.Equal("employees", context.Model.FindEntityType(typeof(Employee))!.GetTableName());
        Assert.Equal("departments", context.Model.FindEntityType(typeof(Department))!.GetTableName());
    }

    [Fact]
    public void ColumnNames_BecomeSnakeCase()
    {
        using var context = CreateContext();
        var employee = context.Model.FindEntityType(typeof(Employee))!;

        Assert.Equal("full_name", employee.FindProperty(nameof(Employee.FullName))!.GetColumnName());
        Assert.Equal("department_id", employee.FindProperty(nameof(Employee.DepartmentId))!.GetColumnName());
        Assert.Equal("id", employee.FindProperty(nameof(Employee.Id))!.GetColumnName());
    }

    // Khoá và chỉ mục cũng phải đổi. Bỏ sót chúng thì mở pgAdmin ra sẽ thấy
    // bảng snake_case nhưng ràng buộc vẫn PascalCase - nhìn rất chắp vá.
    [Fact]
    public void KeyAndIndexNames_BecomeSnakeCase()
    {
        using var context = CreateContext();
        var employee = context.Model.FindEntityType(typeof(Employee))!;

        Assert.Equal("pk_employees", employee.FindPrimaryKey()!.GetName());
        // EF tự tạo thêm chỉ mục cho khoá ngoại, nên ở đây có 2 chỉ mục chứ không phải 1.
        Assert.Contains(employee.GetIndexes(), i => i.GetDatabaseName() == "ix_employees_full_name");
        Assert.Contains(employee.GetIndexes(), i => i.GetDatabaseName() == "ix_employees_department_id");
        Assert.Equal(
            "fk_employees_departments_department_id",
            employee.GetForeignKeys().Single().GetConstraintName());
    }
}

public class SnakeCaseConverterTests
{
    [Theory]
    [InlineData("Employees", "employees")]
    [InlineData("FullName", "full_name")]
    [InlineData("DepartmentId", "department_id")]
    [InlineData("IsDeleted", "is_deleted")]
    [InlineData("Id", "id")]
    // Viết tắt liền nhau: cắt trước chữ hoa CUỐI CÙNG của cụm, không cắt từng chữ một.
    [InlineData("OTPCode", "otp_code")]
    [InlineData("PK_Employees", "pk_employees")]
    [InlineData("IX_Employees_FullName", "ix_employees_full_name")]
    [InlineData("", "")]
    public void ToSnakeCase_Converts(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseNameConverter.ToSnakeCase(input));
    }
}
