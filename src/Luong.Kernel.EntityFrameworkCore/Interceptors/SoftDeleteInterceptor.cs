using Luong.Kernel.Abstractions;
using Luong.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Luong.Kernel.EntityFrameworkCore.Interceptors;

/// <summary>
/// Đổi lệnh xoá thật thành lệnh đánh dấu đã xoá.
///
/// Nhờ nó, code nghiệp vụ cứ viết <c>repository.Remove(employee)</c> như bình thường —
/// tự nhiên nhất, đúng thứ người ta gõ theo phản xạ — nhưng câu lệnh chạm tới database
/// lại là <c>UPDATE ... SET is_deleted = true</c> chứ không phải <c>DELETE</c>.
///
/// Cách còn lại là bắt mọi nơi gọi <c>employee.MarkDeleted()</c>. Chỉ cần một người gõ
/// theo thói quen <c>Remove</c> là mất dữ liệu thật, mà build vẫn xanh và test vẫn qua.
/// </summary>
public sealed class SoftDeleteInterceptor(IDateTimeProvider dateTimeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Convert(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Convert(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Convert(DbContextEventData eventData)
    {
        if (eventData.Context is null)
        {
            return;
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;

        foreach (var entry in eventData.Context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = now;
        }
    }
}
