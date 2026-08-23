using System.Globalization;
using System.Resources;
using Luong.Kernel.Localization;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.AspNetCore.Localization;

/// <summary>
/// Bản cài đặt <see cref="IMessageCatalog"/> đọc từ file <c>.resx</c> nhúng trong assembly.
///
/// <b>Vì sao <c>.resx</c> chứ không phải JSON:</b>
/// <list type="number">
/// <item><c>ResourceManager</c> lo sẵn chuỗi dự phòng theo culture — <c>vi-VN</c> không có
/// thì tự lùi về <c>vi</c>, rồi về mặc định. Tự viết lại chuyện đó là tự tạo thêm chỗ để sai.</item>
/// <item>File được <b>nhúng thẳng vào assembly</b>, nên không có file rời nào để lạc mất
/// lúc triển khai — một loại sự cố bị loại bỏ hoàn toàn.</item>
/// <item>Đây là cách chuẩn của .NET; người mới đọc code không phải học gì thêm.</item>
/// </list>
///
/// Cái giá phải trả: sửa một câu chữ là phải build lại, và file XML đọc diff rất khó.
/// </summary>
public sealed class ResxMessageCatalog(ResourceManager resourceManager, ILogger<ResxMessageCatalog> logger)
    : IMessageCatalog
{
    public string? Find(string code, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            // GetString trả null khi không có khoá — đó là ca BÌNH THƯỜNG, không phải lỗi.
            return resourceManager.GetString(code, culture);
        }
        catch (MissingManifestResourceException exception)
        {
            // Ca này nghĩa là file .resx KHÔNG được nhúng vào assembly — thường do lỗi
            // đóng gói. Nó phải không bao giờ làm hỏng một request: người dùng sẽ nhận
            // câu mặc định viết trong code, vẫn đọc hiểu được.
            //
            // Nhưng phải kêu thật to trong log, vì nó có nghĩa là TOÀN BỘ bản dịch đang
            // không hoạt động. Và AssertUsable() gọi lúc khởi động sẽ chặn trước cả đây.
            logger.LogError(
                exception,
                "Không nạp được tài nguyên dịch. Toàn bộ thông báo sẽ rơi về câu mặc định trong code.");

            return null;
        }
    }

    /// <summary>
    /// Kiểm ngay lúc khởi động: thử đọc một khoá đã biết chắc là có.
    ///
    /// Không có bước này thì lỗi đóng gói .resx chỉ lộ ra khi có người dùng gặp lỗi đầu
    /// tiên — có thể là ba tuần sau, và biểu hiện là một mã kỹ thuật hiện trên màn hình
    /// của khách hàng. Chết ngay lúc khởi động thì người phát hiện là mình.
    /// </summary>
    public void AssertUsable(string knownCode, CultureInfo culture)
    {
        if (Find(knownCode, culture) is null)
        {
            throw new InvalidOperationException(
                $"Tài nguyên dịch không dùng được: không đọc được khoá '{knownCode}' cho '{culture.Name}'. "
                    + "Kiểm tra file .resx đã được đặt EmbeddedResource và tên tài nguyên có đúng không.");
        }
    }
}
