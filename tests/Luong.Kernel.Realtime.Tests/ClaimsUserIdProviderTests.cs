using System.Security.Claims;
using Luong.Kernel.Realtime;

namespace Luong.Kernel.Realtime.Tests;

public class ClaimsUserIdProviderTests
{
    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    // "sub" là tên chuẩn của JWT cho định danh người dùng.
    [Fact]
    public void PrefersTheSubClaim()
    {
        var user = Authenticated(
            new Claim("sub", "user-from-sub"),
            new Claim(ClaimTypes.NameIdentifier, "user-from-nameid"));

        Assert.Equal("user-from-sub", ClaimsUserIdProvider.FromClaims(user));
    }

    // ASP.NET có thói quen tự đổi tên "sub" thành NameIdentifier khi đọc JWT,
    // nên phải chấp nhận cả hai - thiếu nhánh này thì Clients.User(...) im lặng
    // không gửi tới ai, mà không có lỗi nào báo.
    [Fact]
    public void FallsBackToNameIdentifier()
    {
        var user = Authenticated(new Claim(ClaimTypes.NameIdentifier, "user-from-nameid"));

        Assert.Equal("user-from-nameid", ClaimsUserIdProvider.FromClaims(user));
    }

    [Fact]
    public void ReturnsNullWhenNotAuthenticated()
    {
        Assert.Null(ClaimsUserIdProvider.FromClaims(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void ReturnsNullWhenThereIsNoIdClaim()
    {
        Assert.Null(ClaimsUserIdProvider.FromClaims(Authenticated(new Claim("email", "a@b.c"))));
    }

    [Fact]
    public void ReturnsNullForNoUser()
    {
        Assert.Null(ClaimsUserIdProvider.FromClaims(null));
    }
}
