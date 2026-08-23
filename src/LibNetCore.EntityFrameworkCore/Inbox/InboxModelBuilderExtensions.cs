using LibNetCore.Core.Inbox;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Inbox;

/// <summary>Khai báo bảng inbox trong mô hình. Gọi trong <c>OnModelCreating</c>.</summary>
public static class InboxModelBuilderExtensions
{
    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");

            // Khoá chính CHÍNH LÀ EventId. Chọn thế này thì chống trùng được database
            // bảo đảm ở tầng thấp nhất: hai tiến trình cùng xử lý một sự kiện thì đứa
            // thứ hai đâm vào ràng buộc khoá trùng, không cần khoá phân tán nào cả.
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Type).HasMaxLength(300).IsRequired();
        });

        return modelBuilder;
    }
}
