using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace LibNetCore.Jobs;

/// <summary>
/// Chặn cửa vào bảng điều khiển Hangfire.
///
/// ⚠️ <b>Mặc định của Hangfire là CHO PHÉP mọi request từ máy cục bộ, và nhiều hướng dẫn
/// trên mạng bảo "cứ để trống cho nhanh".</b> Để trống nghĩa là bất kỳ ai mở được
/// <c>/hangfire</c> đều xem được tham số của mọi job đã chạy — trong đó thường có mã nhân
/// viên, email, đôi khi cả token — và bấm chạy lại được bất kỳ job nào. Đó là một cửa hậu
/// quản trị mở toang, chứ không phải một trang theo dõi vô hại.
///
/// Lớp này mặc định TỪ CHỐI, chỉ mở cho người đã đăng nhập và có đúng vai trò được nêu.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter(string requiredRole) : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(requiredRole);
    }
}
