using Luong.Kernel.Domain;
using Luong.Kernel.EntityFrameworkCore.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.Tests.Conventions;

file sealed class Role : Entity<Guid>
{
    // Trường sau lưng, tên bắt đầu bằng gạch dưới như mọi trường private khác.
    private string _permissions = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

file sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Không đặt tên rõ ràng thì quy ước sinh ra cột `_permissions` — gạch dưới của
        // C# rò thẳng vào schema database.
        modelBuilder.Entity<Role>()
            .Property<string>("_permissions")
            .HasColumnName("permissions");

        modelBuilder.UseSnakeCaseNames();
    }
}

/// <summary>
/// Quy ước đặt tên phải <b>nhường</b> tên đã khai tay.
///
/// <b>Sự cố có thật, tìm ra ở ONoOffice ngày 2026-08-24:</b> một thuộc tính ánh xạ vào
/// trường sau lưng <c>_permissions</c> được khai <c>HasColumnName("permissions")</c>.
/// Migration sinh ra vẫn tạo cột tên <c>_permissions</c>. Không lỗi, không cảnh báo —
/// tên đã khai chỉ đơn giản là bị vứt đi.
///
/// Nguyên nhân: vòng lặp cột đọc <c>property.Name</c> (tên trong C#) thay vì tên cột
/// đang có. Nghĩa là nó không "bổ sung cho những chỗ chưa đặt tên" như tài liệu nói, mà
/// <b>ghi đè tất</b>. Với cột thường thì hai đường cho cùng kết quả nên chẳng ai thấy gì;
/// nó chỉ lộ ra đúng ở chỗ có người cố tình đặt tên khác — mà đó lại là chỗ người ta có
/// LÝ DO để đặt khác.
///
/// Ba vòng lặp còn lại (bảng, khoá, chỉ mục) vốn đã đọc đúng nguồn từ đầu; chỉ mỗi cột sai.
/// </summary>
public class ExplicitColumnNameTests
{
    private static DbContext CreateContext() =>
        new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite("DataSource=:memory:").Options);

    [Fact]
    public void ExplicitColumnName_SurvivesTheConvention()
    {
        using var context = CreateContext();

        var column = context.Model.FindEntityType(typeof(Role))!.FindProperty("_permissions")!;

        Assert.Equal("permissions", column.GetColumnName());
    }

    // Và quy ước vẫn phải làm việc của nó ở những chỗ không ai đặt tên.
    [Fact]
    public void ConventionStillApplies_WhereNoNameWasGiven()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(Role))!;

        Assert.Equal("display_name", entity.FindProperty(nameof(Role.DisplayName))!.GetColumnName());
        Assert.Equal("roles", entity.GetTableName());
    }
}
