using Luong.Kernel.Messaging.RabbitMq;

namespace Luong.Kernel.Messaging.Tests.RabbitMq;

public class RabbitMqOptionsTests
{
    private static RabbitMqOptions Valid() => new()
    {
        Host = "localhost",
        UserName = "guest",
        Password = "guest",
        Exchange = "hr.events",
    };

    [Fact]
    public void ValidOptions_PassValidation()
    {
        Valid().Validate();
    }

    // Thà chết ngay lúc khởi động với thông báo rõ ràng, còn hơn chạy được rồi
    // im lặng không gửi được gì và mãi sau mới có người phát hiện.
    [Theory]
    [InlineData("Host")]
    [InlineData("UserName")]
    [InlineData("Password")]
    [InlineData("Exchange")]
    public void MissingSetting_FailsFastWithTheFieldName(string missing)
    {
        var options = Valid();
        typeof(RabbitMqOptions).GetProperty(missing)!.SetValue(options, "");

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains(missing, exception.Message);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var options = Valid();

        Assert.Equal(5672, options.Port);
        Assert.Equal("/", options.VirtualHost);
        Assert.Equal(20, options.OutboxBatchSize);
    }
}
