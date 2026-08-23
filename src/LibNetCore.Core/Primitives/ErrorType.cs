namespace LibNetCore.Core.Primitives;

/// <summary>
/// Loại thất bại. Đây là thứ để tầng web quyết định trả mã HTTP nào
/// mà KHÔNG cần biết gì về nghiệp vụ bên trong.
/// </summary>
public enum ErrorType
{
    /// <summary>Hỏng ngoài dự kiến — thường ánh xạ ra HTTP 500.</summary>
    Failure = 0,

    /// <summary>Dữ liệu người gửi không hợp lệ — HTTP 400.</summary>
    Validation = 1,

    /// <summary>Không có thứ được yêu cầu — HTTP 404.</summary>
    NotFound = 2,

    /// <summary>Đụng ràng buộc trạng thái, ví dụ email đã tồn tại — HTTP 409.</summary>
    Conflict = 3,

    /// <summary>Chưa xác định được danh tính — HTTP 401.</summary>
    Unauthorized = 4,

    /// <summary>Biết là ai rồi nhưng không có quyền — HTTP 403.</summary>
    Forbidden = 5,
}
