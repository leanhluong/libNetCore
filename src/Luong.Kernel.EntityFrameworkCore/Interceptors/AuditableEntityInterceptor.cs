using Luong.Kernel.Abstractions;
using Luong.Kernel.Domain;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Luong.Kernel.EntityFrameworkCore.Interceptors;

/// <summary>
/// Tự điền <c>CreatedAtUtc</c> / <c>UpdatedAtUtc</c> ngay trước khi ghi xuống database.
///
/// "Interceptor" nghĩa là một móc treo mà EF gọi hộ ta ở một thời điểm định sẵn — ở đây
/// là ngay trước <c>SaveChanges</c>. Nhờ nó, việc đóng dấu thời gian xảy ra ĐÚNG MỘT CHỖ
/// cho toàn hệ thống, thay vì nằm rải rác trong hàng trăm handler và chờ ai đó quên.
/// </summary>
public sealed class AuditableEntityInterceptor(IDateTimeProvider dateTimeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContextEventData eventData)
    {
        if (eventData.Context is null)
        {
            return;
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;

        foreach (var entry in eventData.Context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case Microsoft.EntityFrameworkCore.EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    break;

                // Cố ý KHÔNG đụng tới CreatedAtUtc ở đây. Ghi đè nó là xoá mất
                // thông tin duy nhất cho biết bản ghi này có từ bao giờ.
                case Microsoft.EntityFrameworkCore.EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    break;

                default:
                    break;
            }
        }
    }
}
