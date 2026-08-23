using Luong.Kernel.Abstractions;
using Luong.Kernel.Domain;
using Luong.Kernel.EntityFrameworkCore.Conventions;
using Luong.Kernel.EntityFrameworkCore.Interceptors;
using Luong.Kernel.EntityFrameworkCore.MultiTenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.Tests.MultiTenancy;

internal sealed class Employee : Entity<Guid>, ITenantScoped, ISoftDeletable
{
    public Employee(Guid id, Guid tenantId, string fullName) : base(id)
    {
        TenantId = tenantId;
        FullName = fullName;
    }

    private Employee() { }

    public Guid TenantId { get; private set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}

/// <summary>Bảng dùng chung, KHÔNG thuộc tenant nào — để chứng minh bộ lọc không đụng nhầm.</summary>
internal sealed class Country : Entity<Guid>
{
    public Country(Guid id, string name) : base(id) => Name = name;

    private Country() { }

    public string Name { get; set; } = string.Empty;
}

internal sealed class HrDbContext(DbContextOptions<HrDbContext> options)
    : DbContext(options), ITenantAwareContext
{
    /// <summary>Đặt được từ test để giả lập "người đang đăng nhập thuộc tenant nào".</summary>
    public Guid CurrentTenantId { get; set; }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Country> Countries => Set<Country>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyTenantQueryFilter(this);
        modelBuilder.ApplySoftDeleteQueryFilter();
        modelBuilder.UseSnakeCaseNames();
        modelBuilder.UseSortableDateTimeOffsets();
    }
}

public class TenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TenantIsolationTests()
    {
        _connection.Open();

        // Gieo sẵn: mỗi tenant một nhân viên, cộng một bảng dùng chung.
        using var seed = CreateContext(TenantA, stampTenant: false);
        seed.Database.EnsureCreated();
        seed.Employees.Add(new Employee(Guid.NewGuid(), TenantA, "Nhân viên của A"));
        seed.Employees.Add(new Employee(Guid.NewGuid(), TenantB, "Nhân viên của B"));
        seed.Countries.Add(new Country(Guid.NewGuid(), "Việt Nam"));
        seed.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private HrDbContext CreateContext(Guid tenantId, bool stampTenant = true)
    {
        var builder = new DbContextOptionsBuilder<HrDbContext>().UseSqlite(_connection);

        if (stampTenant)
        {
            builder.AddInterceptors(new TenantInterceptor(new FixedTenant(tenantId)));
        }

        return new HrDbContext(builder.Options) { CurrentTenantId = tenantId };
    }

    private sealed class FixedTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid? TenantId { get; } = tenantId;
    }

    // ── Đọc ─────────────────────────────────────────────────────────────────

    // Luật cốt lõi: truy vấn BÌNH THƯỜNG chỉ thấy dữ liệu của tenant đang đăng nhập.
    // Không ai phải nhớ viết "WHERE tenant_id = ..." — và vì không phải nhớ, nên không quên được.
    [Fact]
    public async Task TruyVanBinhThuong_ChiThayDuLieuCuaTenantMinh()
    {
        using var context = CreateContext(TenantA);

        var employees = await context.Employees.ToListAsync();

        Assert.Equal("Nhân viên của A", Assert.Single(employees).FullName);
    }

    // Đổi tenant thì kết quả phải đổi theo — chứng minh bộ lọc đọc giá trị SỐNG
    // của context, chứ không bị "đóng băng" vào mô hình lúc khởi động.
    [Fact]
    public async Task DoiTenant_ThiKetQuaDoiTheo()
    {
        using var contextB = CreateContext(TenantB);

        var employees = await contextB.Employees.ToListAsync();

        Assert.Equal("Nhân viên của B", Assert.Single(employees).FullName);
    }

    [Fact]
    public async Task IgnoreQueryFilters_ThayHetMoiTenant()
    {
        using var context = CreateContext(TenantA);

        Assert.Equal(2, (await context.Employees.IgnoreQueryFilters().ToListAsync()).Count);
    }

    // Bảng dùng chung không có tenant_id thì bộ lọc phải để yên.
    // Lọc nhầm nó là mọi truy vấn danh mục trả về rỗng — và rất khó đoán ra nguyên nhân.
    [Fact]
    public async Task BangDungChung_KhongBiLoc()
    {
        using var context = CreateContext(TenantA);

        Assert.Single(await context.Countries.ToListAsync());
    }

    // Hai bộ lọc phải CHỒNG nhau, không được cái sau đè cái trước.
    [Fact]
    public async Task LocTenantVaLocXoaMem_CungCoHieuLuc()
    {
        using var context = CreateContext(TenantA);
        var employee = await context.Employees.SingleAsync();
        employee.IsDeleted = true;
        await context.SaveChangesAsync();

        using var fresh = CreateContext(TenantA);
        Assert.Empty(await fresh.Employees.ToListAsync());
        Assert.Single(await fresh.Employees.IgnoreQueryFilters().Where(e => e.TenantId == TenantA).ToListAsync());
    }

    // ── Ghi ─────────────────────────────────────────────────────────────────

    // Thêm mới thì tenant_id tự điền. Không ai phải gán tay, nên không ai gán sót.
    [Fact]
    public async Task ThemMoi_TuDienTenantId()
    {
        using var context = CreateContext(TenantB);
        context.Employees.Add(new Employee(Guid.NewGuid(), Guid.Empty, "Người mới của B"));

        await context.SaveChangesAsync();

        using var fresh = CreateContext(TenantB);
        Assert.Equal(2, (await fresh.Employees.ToListAsync()).Count);
    }

    // ⭐ LUẬT BẢO MẬT QUAN TRỌNG NHẤT.
    //
    // Cố ghi một hàng mang tenant KHÁC tenant đang đăng nhập thì phải NỔ NGAY, không
    // được lặng lẽ sửa lại giá trị. Sửa lặng lẽ nghĩa là một lỗi lập trình biến thành
    // dữ liệu ghi nhầm chỗ, và không ai biết cho tới khi khách hàng phát hiện.
    [Fact]
    public async Task GhiSangTenantKhac_ThiNemLoiNgay()
    {
        using var context = CreateContext(TenantA);
        context.Employees.Add(new Employee(Guid.NewGuid(), TenantB, "Kẻ đi lạc"));

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());
    }

    // Chưa đăng nhập mà ghi dữ liệu có tenant thì cũng phải nổ — nếu không, hàng đó
    // sẽ mang tenant rỗng và KHÔNG BAO GIỜ hiện ra trong bất kỳ truy vấn nào.
    [Fact]
    public async Task ChuaDangNhapMaGhi_ThiNemLoi()
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new TenantInterceptor(new NoTenant()))
            .Options;

        using var context = new HrDbContext(options) { CurrentTenantId = TenantA };
        context.Employees.Add(new Employee(Guid.NewGuid(), Guid.Empty, "Không rõ của ai"));

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());
    }

    private sealed class NoTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
    }

    [Fact]
    public async Task BangDungChung_ThemMoiBinhThuong()
    {
        using var context = CreateContext(TenantA);
        context.Countries.Add(new Country(Guid.NewGuid(), "Nhật Bản"));

        await context.SaveChangesAsync();

        Assert.Equal(2, (await context.Countries.ToListAsync()).Count);
    }
}
