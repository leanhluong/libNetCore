using System.Security.Claims;
using Luong.Kernel.AspNetCore.Security;
using Luong.Kernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Luong.Kernel.AspNetCore.Tests.Security;

public class HttpContextCurrentUserTests
{
    private static ICurrentUser From(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
        };

        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void UserId_ComesFromTheSubClaim()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, From(new Claim("sub", id.ToString())).UserId);
    }

    // ASP.NET hay tự đổi tên "sub" thành NameIdentifier khi đọc JWT, nên phải nhận cả hai.
    [Fact]
    public void UserId_FallsBackToNameIdentifier()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, From(new Claim(ClaimTypes.NameIdentifier, id.ToString())).UserId);
    }

    // Mã không phải Guid thì trả null chứ KHÔNG ném lỗi. Token hỏng là chuyện của
    // người gọi, không được biến thành lỗi 500 của hệ thống.
    [Fact]
    public void UserId_IsNullWhenTheClaimIsNotAGuid()
    {
        Assert.Null(From(new Claim("sub", "không-phải-guid")).UserId);
    }

    [Fact]
    public void Permissions_ComeFromPermissionClaims()
    {
        var user = From(
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("permission", "employee.read"),
            new Claim("permission", "employee.write"));

        Assert.Equal(2, user.Permissions.Count);
        Assert.True(user.HasPermission("employee.read"));
        Assert.True(user.HasPermission("employee.write"));
        Assert.False(user.HasPermission("employee.delete"));
    }

    // So khớp KHÔNG phân biệt hoa thường: token do một hệ khác phát ra có thể viết
    // "Employee.Read". Phân biệt hoa thường ở đây nghĩa là từ chối oan người có quyền.
    [Fact]
    public void HasPermission_IgnoresCase()
    {
        var user = From(new Claim("permission", "Employee.Read"));

        Assert.True(user.HasPermission("employee.read"));
    }

    [Fact]
    public void UnauthenticatedRequest_HasNoUserAndNoPermissions()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        var user = new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context });

        Assert.Null(user.UserId);
        Assert.False(user.IsAuthenticated);
        Assert.Empty(user.Permissions);
    }

    // Job nền và consumer hàng đợi chạy KHÔNG có HttpContext. Phải trả về
    // "không có ai" thay vì ném NullReferenceException - nếu không, mọi use case
    // dùng ICurrentUser sẽ chết ngay khi chạy ngoài web.
    [Fact]
    public void WithoutAnHttpContext_ThereIsSimplyNoUser()
    {
        var user = new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = null });

        Assert.Null(user.UserId);
        Assert.False(user.IsAuthenticated);
        Assert.Empty(user.Permissions);
        Assert.False(user.HasPermission("employee.read"));
    }
}
