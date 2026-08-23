using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Luong.Kernel.Realtime;

/// <summary>
/// Nói cho SignalR biết "kết nối này là của ai", để gọi được
/// <c>Clients.User(userId).SendAsync(...)</c>.
///
/// Mặc định SignalR lấy <c>ClaimTypes.NameIdentifier</c>. Vấn đề: JWT dùng tên chuẩn là
/// <c>sub</c>, và tuỳ cấu hình mà ASP.NET có đổi tên nó sang <c>NameIdentifier</c> hay
/// không. Chỉ nhận đúng một trong hai là gặp cảnh <c>Clients.User(...)</c> gửi vào hư
/// không — <b>không lỗi, không cảnh báo, tin nhắn chỉ đơn giản là không tới</b>.
/// Nhận cả hai thì hết chuyện đó.
/// </summary>
public sealed class ClaimsUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => FromClaims(connection.User);

    /// <summary>Tách riêng phần thuần logic để test được mà không phải dựng kết nối SignalR.</summary>
    public static string? FromClaims(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
