using System.Linq.Expressions;
using Luong.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.Conventions;

/// <summary>
/// Gắn bộ lọc toàn cục "chỉ lấy hàng chưa xoá" cho MỌI thực thể có đánh dấu
/// <see cref="ISoftDeletable"/>.
///
/// Từ đây <c>context.Employees.ToListAsync()</c> tự khắc không thấy hàng đã xoá — không
/// ai phải nhớ thêm <c>.Where(e =&gt; !e.IsDeleted)</c>. Đây là chỗ khác nhau giữa "có
/// quy tắc" và "có quy tắc được thi hành": quy tắc phải nhớ mới đúng thì sớm muộn cũng
/// có người quên, và lần quên đó là dữ liệu đã xoá lộ ra ngoài.
///
/// Khi thật sự cần nhìn cả hàng đã xoá (màn khôi phục, đối soát) thì gọi rõ ràng
/// <c>.IgnoreQueryFilters()</c> — cố ý bắt phải viết ra, để người đọc code sau này
/// thấy ngay đây là chủ đích chứ không phải sơ suất.
/// </summary>
public static class SoftDeleteModelBuilderExtensions
{
    /// <summary>Tên bộ lọc — EF 10 cho phép nhiều bộ lọc có tên chạy song song trên cùng một thực thể.</summary>
    public const string SoftDeleteFilterKey = "SoftDelete";

    public static ModelBuilder ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Dựng biểu thức e => !e.IsDeleted cho đúng kiểu của thực thể này.
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));

            // EF 10 cho phép nhiều bộ lọc CÓ TÊN cùng tồn tại trên một thực thể. Trước đây
            // API chỉ có một bộ lọc duy nhất, nên gọi hai lần là cái sau đè mất cái trước —
            // bộ lọc tenant biến mất, mọi workspace nhìn thấy dữ liệu của nhau, im lặng.
            entityType.SetQueryFilter(SoftDeleteFilterKey, Expression.Lambda(Expression.Not(isDeleted), parameter));
        }

        return modelBuilder;
    }
}
