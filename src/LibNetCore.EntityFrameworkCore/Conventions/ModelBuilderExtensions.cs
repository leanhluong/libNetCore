using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Conventions;

/// <summary>
/// Áp quy ước đặt tên snake_case cho toàn bộ mô hình bằng MỘT dòng.
///
/// Cách còn lại là gõ tay <c>.HasColumnName("full_name")</c> cho từng cột của từng
/// bảng. Với 20 bảng × 15 cột là 300 dòng chỉ để đặt tên — và chỉ cần một lần quên
/// là có một cột lạc loài PascalCase giữa toàn bảng snake_case.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Gọi ở CUỐI <c>OnModelCreating</c>, sau khi đã khai báo xong quan hệ và chỉ mục —
    /// nó đổi tên những gì đang có, nên thứ khai báo sau nó sẽ không được đổi.
    /// </summary>
    public static ModelBuilder UseSnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            string? tableName = entity.GetTableName();

            if (tableName is not null)
            {
                entity.SetTableName(SnakeCaseNameConverter.ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCaseNameConverter.ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(SnakeCaseNameConverter.ToSnakeCase(key.GetName()!));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(
                    SnakeCaseNameConverter.ToSnakeCase(foreignKey.GetConstraintName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(SnakeCaseNameConverter.ToSnakeCase(index.GetDatabaseName()!));
            }
        }

        return modelBuilder;
    }
}
