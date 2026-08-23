using System.Text;

namespace Luong.Kernel.Caching;

/// <summary>
/// Dựng khoá đệm theo một khuôn thống nhất: <c>onooffice:employee:42</c>.
///
/// Vì sao cần khuôn: Redis là một kho khoá phẳng khổng lồ dùng chung. Mỗi người tự đặt
/// khoá theo ý mình thì vài tháng sau không ai biết khoá nào của ai, không xoá theo nhóm
/// được, và tệ nhất là hai chỗ khác nhau vô tình trùng khoá — lúc đó một bên đọc phải
/// dữ liệu của bên kia, mà kiểu lỗi này gần như không thể lần ra.
///
/// Ba luật: chữ thường hết · ngăn bằng dấu hai chấm · không có khoảng trắng.
/// Dấu hai chấm là thông lệ của Redis và được RedisInsight hiểu như thư mục.
/// </summary>
public static class CacheKey
{
    public static string Build(params string[] segments)
    {
        if (segments.Length == 0)
        {
            throw new ArgumentException("Khoá đệm phải có ít nhất một đoạn.", nameof(segments));
        }

        var builder = new StringBuilder();

        foreach (string segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new ArgumentException("Đoạn khoá đệm không được rỗng.", nameof(segments));
            }

            if (builder.Length > 0)
            {
                builder.Append(':');
            }

            // Khoảng trắng khiến lệnh redis-cli phải bọc nháy — lúc đi dò lỗi rất phiền.
            builder.Append(segment.Trim().ToLowerInvariant().Replace(' ', '-'));
        }

        return builder.ToString();
    }
}
