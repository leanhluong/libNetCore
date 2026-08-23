namespace Luong.Kernel.Abstractions;

/// <summary>
/// Workspace của người đang thực hiện request.
///
/// Tách khỏi <see cref="ICurrentUser"/> có chủ ý: hạ tầng dữ liệu chỉ cần biết ĐANG Ở
/// workspace nào, không cần biết người đó là ai hay có quyền gì. Cổng càng hẹp thì bản
/// giả lập trong test càng nhỏ, và càng ít thứ có thể dùng sai.
///
/// <c>null</c> nghĩa là chưa đăng nhập — hợp lệ với những đường như <c>POST /auth/login</c>.
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
}
