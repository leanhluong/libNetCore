using Microsoft.EntityFrameworkCore;

namespace Luong.Kernel.EntityFrameworkCore.Conventions;

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
                // NHƯỜNG tên đã khai tay bằng HasColumnName.
                //
                // Sự cố có thật (ONoOffice, 2026-08-24): một thuộc tính ánh xạ vào trường
                // sau lưng `_permissions` được khai HasColumnName("permissions"), nhưng
                // migration vẫn sinh ra cột tên `_permissions` — gạch dưới của C# rò
                // thẳng vào schema database. Không lỗi, không cảnh báo, chỉ là tên đã
                // khai bị vứt đi.
                //
                // Nguyên nhân là dòng dưới đây từng đọc property.Name (tên trong C#) chứ
                // không nhìn tên cột đang có. Với cột thường thì hai đường cho cùng kết
                // quả nên chẳng ai thấy gì; nó chỉ lộ ra đúng ở chỗ có người cố tình đặt
                // tên khác — mà đó lại là chỗ người ta có LÝ DO để đặt khác.
                // GetColumnName() trả tên đã khai tay nếu có, không thì trả tên C#. Đưa
                // chính nó vào phép đổi thì cả hai đường đều ra đúng: "FullName" thành
                // "full_name", còn "permissions" (đã khai tay) giữ nguyên "permissions"
                // — phép đổi không làm gì thêm với chuỗi vốn đã snake_case.
                //
                // Cùng một dòng, và đây cũng chính là cách các vòng lặp bảng/khoá/chỉ mục
                // bên dưới vẫn làm từ đầu. Chỉ mỗi cột là đọc nhầm nguồn.
                property.SetColumnName(SnakeCaseNameConverter.ToSnakeCase(property.GetColumnName()));
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
