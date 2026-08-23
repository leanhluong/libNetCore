using System.Security.Claims;
using Luong.Kernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Luong.Kernel.AspNetCore.Security;

/// <summary>
/// Bản cài đặt <see cref="ICurrentUser"/> đọc từ request HTTP đang xử lý.
///
/// <b>Chỉ đọc claim, KHÔNG tự giải mã JWT.</b> Việc kiểm chữ ký và hạn token là của
/// tầng xác thực (hoặc của gateway) — nó đã làm xong trước khi request tới đây. Tự giải
/// mã lại ở tầng ứng dụng là làm hai lần, và tệ hơn: dễ vô tình đọc token mà quên kiểm
/// chữ ký, tức là tin vào thứ do người gọi tự khai.
///
/// <b>Không có HttpContext thì trả về "không có ai", không ném lỗi.</b> Job nền và
/// consumer hàng đợi chạy hoàn toàn ngoài web. Ném lỗi ở đó nghĩa là mọi use case có
/// dùng <see cref="ICurrentUser"/> sẽ chết ngay khi chạy ngoài request — mà đó lại
/// chính là lý do <see cref="ICurrentUser"/> được đặt ở <c>Core</c>.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    /// <summary>Tên claim chứa quyền. Đổi được nếu hệ phát token dùng tên khác.</summary>
    public const string PermissionClaimType = "permission";

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            string? value = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Mã không đúng dạng thì coi như không có, KHÔNG ném lỗi. Token hỏng là
            // chuyện của người gọi (đáng ra là 401), không được thành lỗi 500 của hệ thống.
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public IReadOnlySet<string> Permissions =>
        IsAuthenticated
            ? User!.FindAll(PermissionClaimType)
                .Select(claim => claim.Value)

                // So khớp không phân biệt hoa thường: token do hệ khác phát có thể viết
                // "Employee.Read". Phân biệt hoa thường ở đây là từ chối oan người có quyền.
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
