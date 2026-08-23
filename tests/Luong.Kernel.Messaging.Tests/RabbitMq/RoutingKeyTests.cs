using Luong.Kernel.Messaging.RabbitMq;

namespace Luong.Kernel.Messaging.Tests.RabbitMq;

public class RoutingKeyTests
{
    // Tên kiểu đầy đủ rất dài. Khoá định tuyến chỉ lấy đoạn CUỐI, vì đó là thứ
    // bên nhận quan tâm - và cũng để đổi namespace bên gửi không làm gãy binding bên nhận.
    [Theory]
    [InlineData("ONoOffice.Hr.Events.EmployeeHired", "employee-hired")]
    [InlineData("EmployeeHired", "employee-hired")]
    [InlineData("Hr.LeaveRequestApproved", "leave-request-approved")]
    [InlineData("Hr.OTPRequested", "otp-requested")]
    public void From_TakesTheLastSegmentInKebabCase(string eventType, string expected)
    {
        Assert.Equal(expected, RoutingKey.From(eventType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hr.")]
    public void From_RejectsAnEmptyName(string eventType)
    {
        Assert.Throws<ArgumentException>(() => RoutingKey.From(eventType));
    }
}
