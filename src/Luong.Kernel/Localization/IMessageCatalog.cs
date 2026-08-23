using System.Globalization;

namespace Luong.Kernel.Localization;

/// <summary>
/// Tra câu chữ đã dịch theo mã.
///
/// Cổng cố tình HẸP: chỉ một phép tra, trả <c>null</c> khi không có. Người gọi tự quyết
/// định dùng gì thay thế — và trong hệ này, thứ thay thế luôn là câu mặc định viết trong
/// code, không bao giờ là mã kỹ thuật trần.
///
/// Không có phương thức "dịch bắt buộc phải có" là chủ ý: thiếu bản dịch KHÔNG được phép
/// làm hỏng request. Việc phát hiện thiếu là của test, không phải của người dùng cuối.
/// </summary>
public interface IMessageCatalog
{
    /// <summary><c>null</c> nghĩa là chưa có bản dịch cho mã này ở ngôn ngữ đó.</summary>
    string? Find(string code, CultureInfo culture);
}
