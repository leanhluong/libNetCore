namespace Luong.Kernel.Abstractions;

/// <summary>
/// Nguồn thời gian duy nhất của hệ thống.
///
/// Vì sao không gọi thẳng <c>DateTime.Now</c> — hai lý do, cả hai đều đau:
///
/// 1. <b>Không test được.</b> Muốn kiểm "token hết hạn sau 15 phút" mà gọi thẳng đồng hồ
///    hệ thống thì chỉ còn cách ngồi chờ 15 phút. Có lớp trung gian này thì test đưa vào
///    một đồng hồ giả, nhảy thẳng tới tương lai.
///
/// 2. <b><c>DateTime.Now</c> lấy giờ MÁY CHỦ.</b> Máy chủ đặt ở Singapore, người dùng ở
///    Hà Nội, bản sao lưu chạy ở Frankfurt — ba nơi ba giờ khác nhau. Chỉ UTC mới so sánh
///    được. Dùng <see cref="DateTimeOffset"/> chứ không <c>DateTime</c> vì nó mang theo
///    độ lệch múi giờ, không để người đọc sau này phải đoán.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Bản dùng thật khi chạy. Test thay bằng đồng hồ giả.</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
