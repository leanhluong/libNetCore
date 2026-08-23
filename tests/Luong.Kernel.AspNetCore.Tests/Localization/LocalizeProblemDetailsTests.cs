using System.Globalization;
using Luong.Kernel.AspNetCore.Errors;
using Luong.Kernel.Localization;
using Luong.Kernel.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Luong.Kernel.AspNetCore.Tests.Localization;

internal sealed class FakeCatalog : Dictionary<(string Code, string Culture), string>, IMessageCatalog
{
    public string? Find(string code, CultureInfo culture) =>
        TryGetValue((code, culture.Name), out string? value) ? value : null;
}

public class LocalizeProblemDetailsTests
{
    private static readonly CultureInfo En = new("en");
    private static readonly CultureInfo Vi = new("vi");

    private static IReadOnlyList<ErrorDetail> DetailsOf(ProblemDetails problem) =>
        (IReadOnlyList<ErrorDetail>)problem.Extensions["errors"]!;

    private static ProblemDetails Localized(Error error, IMessageCatalog? catalog, CultureInfo? culture = null) =>
        error.ToProblemDetails().Localize(catalog, culture);

    [Fact]
    public void CoBanDich_ThiThayCauChu()
    {
        var catalog = new FakeCatalog { [("Auth.InvalidCredentials", "en")] = "Wrong email or password." };
        var error = Error.Unauthorized("Auth.InvalidCredentials", "Email hoặc mật khẩu không đúng.");

        var problem = Localized(error, catalog, En);

        Assert.Equal("Wrong email or password.", Assert.Single(DetailsOf(problem)).Description);
    }

    // ⭐ Thiếu bản dịch thì GIỮ câu mặc định trong code, KHÔNG trả về mã trần.
    //
    // Trả về "Auth.InvalidCredentials" là đẩy một chuỗi kỹ thuật lên màn hình khách hàng.
    // Giữ câu mặc định thì họ vẫn đọc hiểu, còn chỗ thiếu bản dịch để test bắt ở CI —
    // đúng thứ tự trách nhiệm: máy phát hiện, không phải người dùng.
    [Fact]
    public void ThieuBanDich_ThiGiuCauMacDinh()
    {
        var error = Error.Unauthorized("Auth.InvalidCredentials", "Email hoặc mật khẩu không đúng.");

        var problem = Localized(error, new FakeCatalog(), En);

        Assert.Equal("Email hoặc mật khẩu không đúng.", Assert.Single(DetailsOf(problem)).Description);
    }

    // Mã lỗi là hợp đồng với frontend — dịch KHÔNG được đụng vào nó.
    [Fact]
    public void MaLoi_KhongDoi_ChiCauChuDoi()
    {
        var catalog = new FakeCatalog { [("Dept.NotFound", "en")] = "Department not found." };

        var problem = Localized(Error.NotFound("Dept.NotFound", "Không tìm thấy phòng ban."), catalog, En);

        Assert.Equal("Dept.NotFound", Assert.Single(DetailsOf(problem)).Code);
    }

    // Lỗi kiểm dữ liệu mang nhiều lỗi con — phải dịch HẾT, không sót cái nào.
    [Fact]
    public void LoiNhieuDong_ThiDichHet()
    {
        var catalog = new FakeCatalog
        {
            [("User.EmailInvalid", "en")] = "Email is not valid.",
            [("User.PhoneInvalid", "en")] = "Phone number is not valid.",
        };

        var error = new ValidationError(
        [
            Error.Validation("User.EmailInvalid", "Email không hợp lệ."),
            Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ."),
        ]);

        var details = DetailsOf(Localized(error, catalog, En));

        Assert.Equal(2, details.Count);
        Assert.Contains(details, d => d.Description == "Email is not valid.");
        Assert.Contains(details, d => d.Description == "Phone number is not valid.");
    }

    // Dịch được một nửa vẫn phải chạy: cái nào có bản dịch thì dịch, cái nào chưa thì giữ.
    [Fact]
    public void DichDuocMotNua_ThiVanChay()
    {
        var catalog = new FakeCatalog { [("User.EmailInvalid", "en")] = "Email is not valid." };

        var error = new ValidationError(
        [
            Error.Validation("User.EmailInvalid", "Email không hợp lệ."),
            Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ."),
        ]);

        var details = DetailsOf(Localized(error, catalog, En));

        Assert.Contains(details, d => d.Description == "Email is not valid.");
        Assert.Contains(details, d => d.Description == "Số điện thoại không hợp lệ.");
    }

    // Không cấu hình dịch thì hệ thống vẫn chạy đúng, chỉ là bằng tiếng mặc định.
    [Fact]
    public void KhongCoCatalog_ThiGiuNguyenTatCa()
    {
        var problem = Localized(Error.NotFound("Dept.NotFound", "Không tìm thấy phòng ban."), catalog: null);

        Assert.Equal("Không tìm thấy phòng ban.", Assert.Single(DetailsOf(problem)).Description);
    }

    [Fact]
    public void DoiCulture_ThiDoiCauChu()
    {
        var catalog = new FakeCatalog
        {
            [("Dept.NotFound", "en")] = "Department not found.",
            [("Dept.NotFound", "vi")] = "Không tìm thấy phòng ban.",
        };
        var error = Error.NotFound("Dept.NotFound", "câu mặc định");

        Assert.Equal("Department not found.", Assert.Single(DetailsOf(Localized(error, catalog, En))).Description);
        Assert.Equal("Không tìm thấy phòng ban.", Assert.Single(DetailsOf(Localized(error, catalog, Vi))).Description);
    }
}
