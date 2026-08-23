using LibNetCore.Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibNetCore.EntityFrameworkCore.Outbox;

/// <summary>Khai báo bảng outbox trong mô hình. Gọi trong <c>OnModelCreating</c>.</summary>
public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Type).HasMaxLength(300).IsRequired();
            builder.Property(m => m.Content).IsRequired();
            builder.Property(m => m.Error).HasMaxLength(2000);

            // Job nền luôn hỏi đúng một câu: "còn hàng nào CHƯA gửi không, cũ nhất trước".
            // Không có chỉ mục này thì mỗi vòng quét là một lần đọc toàn bảng — và bảng
            // outbox là bảng lớn nhanh nhất trong hệ thống.
            builder
                .HasIndex(m => new { m.ProcessedOnUtc, m.OccurredOnUtc })
                .HasDatabaseName("IX_OutboxMessages_Unprocessed");
        });

        return modelBuilder;
    }
}
