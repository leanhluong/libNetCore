namespace Luong.Kernel.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// <c>DbContext</c> nào biết mình đang phục vụ workspace nào thì cài interface này.
///
/// Vì sao bộ lọc phải đọc từ CONTEXT chứ không nhận một <c>Guid</c> lúc dựng mô hình:
/// EF dựng mô hình MỘT lần rồi cache lại cho cả tiến trình. Nhét thẳng một giá trị vào
/// lúc đó thì mọi request sau đều dùng lại đúng tenant của request ĐẦU TIÊN — nghĩa là
/// người dùng thứ hai sẽ nhìn thấy dữ liệu của người thứ nhất.
///
/// Đọc qua thuộc tính của context thì EF biến nó thành tham số truy vấn và lấy giá trị
/// SỐNG ở mỗi lần chạy.
/// </summary>
public interface ITenantAwareContext
{
    Guid CurrentTenantId { get; }
}
