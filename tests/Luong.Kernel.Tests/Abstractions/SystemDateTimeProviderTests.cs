using Luong.Kernel.Abstractions;

namespace Luong.Kernel.Tests.Abstractions;

public class SystemDateTimeProviderTests
{
    // Luôn UTC, không bao giờ giờ máy chủ. Máy chủ ở Singapore, người dùng ở Hà Nội,
    // log ở Frankfurt - chỉ có UTC mới so sánh được với nhau.
    [Fact]
    public void UtcNow_HasZeroOffset()
    {
        IDateTimeProvider provider = new SystemDateTimeProvider();

        Assert.Equal(TimeSpan.Zero, provider.UtcNow.Offset);
    }

    [Fact]
    public void UtcNow_IsTheCurrentTime()
    {
        IDateTimeProvider provider = new SystemDateTimeProvider();

        var difference = (provider.UtcNow - DateTimeOffset.UtcNow).Duration();

        Assert.True(difference < TimeSpan.FromSeconds(5), $"Lệch {difference} so với đồng hồ hệ thống.");
    }
}
