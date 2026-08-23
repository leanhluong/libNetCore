namespace Luong.Kernel.Domain;

/// <summary>
/// Đánh dấu thực thể THUỘC VỀ một workspace (tenant).
///
/// Chỉ có phần đọc. Việc điền <see cref="TenantId"/> là của hạ tầng, không phải của
/// nghiệp vụ — nếu để nghiệp vụ gán tay thì sớm muộn có chỗ quên, và hàng đó sẽ mang
/// tenant rỗng rồi biến mất khỏi mọi truy vấn.
///
/// Hạ tầng ghi vào được dù thuộc tính chỉ có <c>private set</c>, vì nó đi qua bộ theo dõi
/// thay đổi của EF chứ không đi qua thuộc tính C#.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
