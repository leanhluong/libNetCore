using LibNetCore.AspNetCore.Errors;
using LibNetCore.Core.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace LibNetCore.AspNetCore.Tests.Errors;

public class ErrorToProblemDetailsTests
{
    // Ánh xạ loại lỗi -> mã HTTP. Đây là TOÀN BỘ lý do ErrorType tồn tại:
    // tầng web quyết định mã HTTP mà không cần biết một chữ nào về nghiệp vụ.
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Failure, 500)]
    public void ErrorType_MapsToTheRightStatusCode(ErrorType type, int expected)
    {
        var error = new Error("Some.Code", "Mô tả.", type);

        Assert.Equal(expected, error.ToProblemDetails().Status);
    }

    [Fact]
    public void ProblemDetails_CarriesTheErrorCodeAndDescription()
    {
        var error = Error.Conflict("Employee.EmailTaken", "Email đã có người dùng.");

        var problem = error.ToProblemDetails();

        var errors = Assert.IsAssignableFrom<IReadOnlyList<ErrorDetail>>(problem.Extensions["errors"]);
        var only = Assert.Single(errors);
        Assert.Equal("Employee.EmailTaken", only.Code);
        Assert.Equal("Email đã có người dùng.", only.Description);
    }

    // ValidationError ôm nhiều lỗi con -> tất cả phải hiện ra, không được rút còn 1.
    [Fact]
    public void ValidationError_ListsEveryChildError()
    {
        var error = new ValidationError(
        [
            Error.Validation("User.EmailInvalid", "Email không hợp lệ."),
            Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ."),
        ]);

        var problem = error.ToProblemDetails();

        var errors = Assert.IsAssignableFrom<IReadOnlyList<ErrorDetail>>(problem.Extensions["errors"]);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Code == "User.EmailInvalid");
        Assert.Contains(errors, e => e.Code == "User.PhoneInvalid");
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public void ProblemDetails_HasATitleAndATypeUri()
    {
        var problem = Error.NotFound("Dept.NotFound", "Không tìm thấy phòng ban.").ToProblemDetails();

        Assert.Equal("Not Found", problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
    }

    // Result thành công không phải là lỗi -> gọi hàm này là lập trình sai.
    [Fact]
    public void ErrorNone_CannotBecomeProblemDetails()
    {
        Assert.Throws<InvalidOperationException>(() => Error.None.ToProblemDetails());
    }
}
