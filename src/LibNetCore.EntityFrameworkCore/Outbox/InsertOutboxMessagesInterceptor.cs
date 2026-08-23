using System.Text.Encodings.Web;
using System.Text.Json;
using LibNetCore.Core.Domain;
using LibNetCore.Core.Outbox;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LibNetCore.EntityFrameworkCore.Outbox;

/// <summary>
/// Ngay trước khi lưu: gom mọi domain event đang treo trên các gốc tổng hợp, biến thành
/// hàng trong bảng outbox, rồi lưu CHUNG một transaction với dữ liệu nghiệp vụ.
///
/// Đây là chỗ khép lại vòng thiết kế:
/// <list type="number">
/// <item>Tầng nghiệp vụ chỉ <c>Raise(new EmployeeHired(...))</c> — không biết RabbitMQ là gì.</item>
/// <item>Interceptor này biến sự kiện thành hàng dữ liệu, vẫn không gửi đi đâu cả.</item>
/// <item>Job nền đọc bảng, gửi thật, rồi đánh dấu đã xử lý.</item>
/// </list>
///
/// Nhờ vậy nếu RabbitMQ đang chết thì nghiệp vụ VẪN chạy bình thường — sự kiện nằm chờ
/// trong database, RabbitMQ sống lại là đi tiếp. Gửi thẳng thì nghiệp vụ chết theo hàng đợi.
/// </summary>
public sealed class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // CỐ Ý không dùng JsonSerializerDefaults.Web (camelCase). Nội dung outbox là kho
        // lưu NỘI BỘ, không phải payload gửi cho trình duyệt. Giữ nguyên tên thuộc tính
        // C# thì bên đọc chỉ cần JsonSerializer.Deserialize<T> mặc định là ra — không
        // phải nhớ truyền đúng bộ tuỳ chọn. Quên bộ tuỳ chọn thì kết quả là một đối
        // tượng RỖNG mà không có lỗi nào báo: đúng loại lỗi tệ nhất, im lặng và sai.
        //
        // Encoder nới lỏng để tiếng Việt nằm nguyên dạng trong bảng. Mặc định sẽ ghi
        // thành "Lê Anh Lượng" — vừa phình dung lượng, vừa khiến lúc mở
        // bảng ra dò lỗi thì không đọc nổi.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CollectDomainEvents(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CollectDomainEvents(DbContextEventData eventData)
    {
        if (eventData.Context is null)
        {
            return;
        }

        var messages = new List<OutboxMessage>();

        foreach (var entry in eventData.Context.ChangeTracker.Entries<IHasDomainEvents>())
        {
            var domainEvents = entry.Entity.DomainEvents.ToList();

            if (domainEvents.Count == 0)
            {
                continue;
            }

            // Dọn ngay tại đây. Quên dọn thì lần lưu sau sẽ đẩy lại đúng những
            // sự kiện cũ - người nhận lĩnh "nhân viên vừa được tuyển" hai lần.
            entry.Entity.ClearDomainEvents();

            messages.AddRange(domainEvents.Select(ToOutboxMessage));
        }

        if (messages.Count > 0)
        {
            eventData.Context.Set<OutboxMessage>().AddRange(messages);
        }
    }

    private static OutboxMessage ToOutboxMessage(IDomainEvent domainEvent) => new()
    {
        Id = Guid.NewGuid(),
        Type = domainEvent.GetType().FullName!,

        // Bắt buộc truyền kiểu THẬT lúc chạy. Bỏ tham số này thì System.Text.Json chỉ
        // nhìn thấy IDomainEvent và ghi ra JSON chỉ có EventId - mất sạch dữ liệu riêng
        // của sự kiện, mà không hề báo lỗi.
        Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),

        OccurredOnUtc = domainEvent.OccurredOnUtc,
    };
}
