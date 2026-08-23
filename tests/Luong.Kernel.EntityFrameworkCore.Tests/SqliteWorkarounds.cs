using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Luong.Kernel.EntityFrameworkCore.Tests;

/// <summary>
/// Vá một khác biệt THẬT giữa provider dùng để test và provider chạy thật.
///
/// SQLite không có kiểu ngày giờ riêng — nó lưu mọi thứ thành chữ hoặc số — nên EF
/// từ chối dịch <c>ORDER BY</c> trên <see cref="DateTimeOffset"/>. PostgreSQL thì có
/// <c>timestamptz</c> thật và sắp xếp bình thường, nên KHÔNG cần gì cả.
///
/// Ở đây quy đổi sang số tick để sắp xếp được. Đây là thứ CHỈ dùng trong test — nó
/// nằm ở project test chứ không nằm trong thư viện, để không ai lỡ mang một cách lưu
/// dành cho SQLite vào hệ thống chạy Postgres.
///
/// Bài học đi kèm: SQLite là "gần giống" Postgres chứ không phải "giống hệt". Nó bắt
/// được phần lớn lỗi, nhưng vẫn phải chạy migration thật trên Postgres trước khi tin.
/// </summary>
internal static class SqliteWorkarounds
{
    private static readonly ValueConverter<DateTimeOffset, long> Converter =
        new(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableConverter =
        new(
            value => value.HasValue ? value.Value.UtcTicks : null,
            ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : null);

    public static ModelBuilder UseSortableDateTimeOffsets(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(Converter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableConverter);
                }
            }
        }

        return modelBuilder;
    }
}
