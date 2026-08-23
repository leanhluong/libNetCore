using Luong.Kernel.AspNetCore.Errors;
using Luong.Kernel.AspNetCore.Results;
using Luong.Kernel.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Luong.Kernel.AspNetCore.Tests.Results;

public class ResultActionExtensionsTests
{
    // Thao tác không trả dữ liệu (xoá, đổi phòng ban) mà thành công thì 204,
    // không phải 200 kèm thân rỗng.
    [Fact]
    public void Success_WithoutValue_Gives204()
    {
        var action = Result.Success().ToActionResult();

        Assert.IsType<NoContentResult>(action);
    }

    [Fact]
    public void Success_WithValue_Gives200AndTheValue()
    {
        var action = Result.Success("Lê Anh Lượng").ToActionResult();

        var ok = Assert.IsType<OkObjectResult>(action);
        Assert.Equal("Lê Anh Lượng", ok.Value);
    }

    [Fact]
    public void Created_ReturnsLocationAndValue()
    {
        var id = Guid.NewGuid();

        var action = Result.Success(id).ToCreatedResult($"/api/employees/{id}");

        var created = Assert.IsType<CreatedResult>(action);
        Assert.Equal($"/api/employees/{id}", created.Location);
        Assert.Equal(id, created.Value);
    }

    // Thất bại thì KHÔNG trả 201 dù đã gọi ToCreatedResult - mã lỗi vẫn thắng.
    [Fact]
    public void Created_OnFailure_StillReturnsTheError()
    {
        var action = Result.Failure<Guid>(Error.Conflict("Employee.EmailTaken", "Email đã dùng."))
            .ToCreatedResult("/api/employees/1");

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Failure, 500)]
    public void Failure_UsesTheStatusCodeOfTheErrorType(ErrorType type, int expected)
    {
        var action = Result.Failure(new Error("Some.Code", "Mô tả.", type)).ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expected, objectResult.StatusCode);
    }

    [Fact]
    public void Failure_CarriesProblemDetailsWithTheErrorCode()
    {
        var action = Result.Failure<string>(Error.NotFound("Dept.NotFound", "Không tìm thấy phòng ban."))
            .ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(action);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

        var errors = Assert.IsAssignableFrom<IReadOnlyList<ErrorDetail>>(problem.Extensions["errors"]);
        Assert.Equal("Dept.NotFound", Assert.Single(errors).Code);
    }

    // Nhiều lỗi kiểm dữ liệu phải hiện ra HẾT, không rút còn một.
    [Fact]
    public void ValidationFailure_ListsEveryError()
    {
        var error = new ValidationError(
        [
            Error.Validation("User.EmailInvalid", "Email không hợp lệ."),
            Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ."),
        ]);

        var action = Result.Failure(error).ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(400, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        var errors = Assert.IsAssignableFrom<IReadOnlyList<ErrorDetail>>(problem.Extensions["errors"]);
        Assert.Equal(2, errors.Count);
    }

    // Phản hồi lỗi phải mang đúng content-type của RFC 7807, không phải application/json trơn.
    [Fact]
    public void Failure_UsesProblemJsonContentType()
    {
        var action = Result.Failure(Error.Failure("Db.Down", "Mất kết nối.")).ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Contains("application/problem+json", objectResult.ContentTypes);
    }
}
