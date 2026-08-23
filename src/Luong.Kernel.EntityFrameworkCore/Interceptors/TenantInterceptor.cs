using Luong.Kernel.Abstractions;
using Luong.Kernel.Domain;
using Luong.Kernel.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Luong.Kernel.EntityFrameworkCore.Interceptors;

/// <summary>
/// Lớp thứ ba của cô lập tenant: canh chiều GHI.
///
/// Bộ lọc toàn cục chỉ canh chiều ĐỌC. Thiếu lớp này thì vẫn còn hai lỗ:
///
/// <list type="number">
/// <item><b>Thêm mới mà quên gán tenant</b> → hàng đó mang tenant rỗng và KHÔNG BAO GIỜ
/// hiện ra trong bất kỳ truy vấn nào. Dữ liệu vẫn nằm trong bảng, chỉ là vô hình — kiểu
/// hỏng rất khó phát hiện vì không có lỗi nào cả.</item>
/// <item><b>Ghi sang tenant khác</b> → rò rỉ dữ liệu, và tệ hơn: ghi bậy vào công ty khác.</item>
/// </list>
/// </summary>
public sealed class TenantInterceptor(ICurrentTenant currentTenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Enforce(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Enforce(DbContextEventData eventData)
    {
        if (eventData.Context is null)
        {
            return;
        }

        foreach (var entry in eventData.Context.ChangeTracker.Entries<ITenantScoped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    StampOrReject(entry);
                    break;

                // Sửa/xoá một hàng của tenant khác đáng ra không xảy ra được, vì bộ lọc
                // toàn cục đã chặn ở khâu đọc. Vẫn canh ở đây vì có những đường vòng:
                // IgnoreQueryFilters, Attach một thực thể dựng tay, hoặc truy vấn SQL thô.
                case EntityState.Modified:
                case EntityState.Deleted:
                    RejectIfForeign(entry);
                    break;

                default:
                    break;
            }
        }
    }

    private void StampOrReject(EntityEntry<ITenantScoped> entry)
    {
        Guid? current = currentTenant.TenantId;

        if (entry.Entity.TenantId == Guid.Empty)
        {
            if (current is null || current == Guid.Empty)
            {
                throw new CrossTenantWriteException(
                    $"Đang ghi '{entry.Entity.GetType().Name}' mà không xác định được workspace. "
                        + "Thao tác này phải chạy trong một request đã đăng nhập.");
            }

            // Ghi qua bộ theo dõi thay đổi của EF, KHÔNG qua thuộc tính C# — nhờ vậy
            // ITenantScoped chỉ cần lộ phần đọc, và tầng nghiệp vụ không gán tay được.
            entry.Property(nameof(ITenantScoped.TenantId)).CurrentValue = current.Value;

            return;
        }

        if (current is not null && entry.Entity.TenantId != current)
        {
            throw new CrossTenantWriteException(
                $"Đang ghi '{entry.Entity.GetType().Name}' thuộc workspace {entry.Entity.TenantId} "
                    + $"trong khi phiên hiện tại thuộc workspace {current}.");
        }
    }

    private void RejectIfForeign(EntityEntry<ITenantScoped> entry)
    {
        Guid? current = currentTenant.TenantId;

        if (current is not null && entry.Entity.TenantId != current)
        {
            throw new CrossTenantWriteException(
                $"Đang sửa/xoá '{entry.Entity.GetType().Name}' thuộc workspace {entry.Entity.TenantId} "
                    + $"trong khi phiên hiện tại thuộc workspace {current}.");
        }
    }
}
