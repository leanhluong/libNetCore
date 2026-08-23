using System.Linq.Expressions;
using Luong.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Gắn bộ lọc "chỉ lấy dữ liệu của workspace đang đăng nhập" cho MỌI thực thể có đánh
/// dấu <see cref="ITenantScoped"/>.
///
/// Đây là lớp thứ hai trong bốn lớp cô lập tenant (xem <c>ADR-0001</c> của ONoOffice):
///
/// <code>
///   1. ITenantScoped        · đánh dấu thực thể nào có tenant
///   2. bộ lọc toàn cục      · MỌI truy vấn tự thêm điều kiện  ← lớp này
///   3. TenantInterceptor    · INSERT tự điền, ghi lệch tenant thì nổ
///   4. test cô lập          · chứng minh tenant B không thấy dữ liệu tenant A
/// </code>
///
/// Vì sao phải là bộ lọc toàn cục thay vì bắt mỗi truy vấn tự thêm <c>Where</c>: quy tắc
/// nào phải NHỚ mới đúng thì sớm muộn cũng có người quên — và ở đây, lần quên đó là rò
/// rỉ dữ liệu giữa hai công ty khác nhau.
/// </summary>
public static class TenantModelBuilderExtensions
{
    /// <summary>Tên bộ lọc tenant. Dùng để bỏ qua riêng nó bằng IgnoreQueryFilters(["Tenant"]).</summary>
    public const string TenantFilterKey = "Tenant";

    /// <summary>
    /// Gọi trong <c>OnModelCreating</c>, truyền chính <c>this</c>:
    /// <code>modelBuilder.ApplyTenantQueryFilter(this);</code>
    /// </summary>
    public static ModelBuilder ApplyTenantQueryFilter(this ModelBuilder modelBuilder, ITenantAwareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                // Bảng dùng chung (danh mục quốc gia, tỉnh thành…) không thuộc tenant nào.
                // Lọc nhầm chúng thì mọi truy vấn danh mục trả về rỗng — và rất khó đoán
                // ra nguyên nhân, vì code truy vấn nhìn hoàn toàn bình thường.
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");

            var body = Expression.Equal(
                Expression.Property(parameter, nameof(ITenantScoped.TenantId)),

                // Đọc từ ĐỐI TƯỢNG context, không phải một Guid chốt cứng. EF biến chỗ
                // này thành tham số truy vấn và lấy giá trị SỐNG ở mỗi lần chạy — nhờ
                // vậy mô hình cache dùng chung được cho mọi tenant.
                Expression.Property(
                    Expression.Constant(context),
                    nameof(ITenantAwareContext.CurrentTenantId)));

            // Đặt TÊN cho bộ lọc để nó sống song song với bộ lọc xoá mềm.
            // API một-bộ-lọc-duy-nhất đời cũ là GÁN ĐÈ: gọi hai lần thì cái sau xoá mất
            // cái trước — bộ lọc tenant biến mất, mọi workspace nhìn thấy dữ liệu của
            // nhau, im lặng, không lỗi nào báo.
            entityType.SetQueryFilter(TenantFilterKey, Expression.Lambda(body, parameter));
        }

        return modelBuilder;
    }
}
