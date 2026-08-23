namespace LibNetCore.Core.Abstractions;

/// <summary>
/// Người đang thực hiện request hiện tại.
///
/// Vì sao là interface nằm ở <c>Core</c> chứ không đọc thẳng <c>HttpContext</c>:
/// tầng Application cần biết "ai đang gọi" để kiểm quyền và ghi vết, nhưng nó
/// KHÔNG được biết HTTP là gì — cũng chính use case đó phải chạy được từ một job nền
/// hay một consumer hàng đợi, nơi không hề có <c>HttpContext</c>.
/// Bản cài đặt đọc từ HTTP nằm ở <c>LibNetCore.AspNetCore</c>.
/// </summary>
public interface ICurrentUser
{
    /// <summary><c>null</c> khi request chưa đăng nhập.</summary>
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    /// <summary>Danh sách quyền, dạng <c>employee.read</c>, <c>employee.write</c>.</summary>
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
}
