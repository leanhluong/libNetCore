using Luong.Kernel.Text;

namespace Luong.Kernel.Messaging.RabbitMq;

/// <summary>
/// Dựng khoá định tuyến từ tên kiểu của sự kiện.
///
/// <code>
///   ONoOffice.Hr.Events.EmployeeHired   →   employee-hired
/// </code>
///
/// Chỉ lấy đoạn CUỐI, có chủ ý: bên nhận chỉ quan tâm "chuyện gì xảy ra", không quan
/// tâm bên gửi xếp lớp thư mục thế nào. Nếu nhét cả namespace vào khoá thì hôm nào bên
/// gửi đổi tên thư mục là mọi binding bên nhận gãy hết — mà gãy im lặng: sự kiện vẫn
/// được gửi, chỉ là không rơi vào hàng đợi nào cả.
/// </summary>
public static class RoutingKey
{
    public static string From(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Tên kiểu sự kiện không được rỗng.", nameof(eventType));
        }

        string lastSegment = eventType.Split('.')[^1];

        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            throw new ArgumentException($"Không lấy được tên sự kiện từ '{eventType}'.", nameof(eventType));
        }

        return CaseConverter.ToKebabCase(lastSegment);
    }
}
