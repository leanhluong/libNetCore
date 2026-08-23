using Luong.Kernel.Primitives;

namespace Luong.Kernel.Tests.Primitives;

public class ValidationErrorTests
{
    [Fact]
    public void ValidationError_HasValidationType()
    {
        var error = new ValidationError([Error.Validation("A", "a")]);

        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void ValidationError_CarriesEveryError()
    {
        var email = Error.Validation("User.EmailInvalid", "Email không hợp lệ.");
        var phone = Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ.");

        var error = new ValidationError([email, phone]);

        Assert.Equal(2, error.Errors.Length);
        Assert.Contains(email, error.Errors);
        Assert.Contains(phone, error.Errors);
    }

    // Kiểm 5 ô một lượt rồi gom lỗi lại — chứ không dừng ở ô sai đầu tiên.
    [Fact]
    public void FromResults_CollectsOnlyTheFailures()
    {
        var email = Error.Validation("User.EmailInvalid", "Email không hợp lệ.");
        var phone = Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ.");

        var error = ValidationError.FromResults(
        [
            Result.Success(),
            Result.Failure(email),
            Result.Success(),
            Result.Failure(phone),
        ]);

        Assert.Equal([email, phone], error.Errors);
    }

    [Fact]
    public void ValidationError_FitsInsideAResult()
    {
        var error = new ValidationError([Error.Validation("A", "a")]);

        Result result = Result.Failure(error);

        Assert.True(result.IsFailure);
        var carried = Assert.IsType<ValidationError>(result.Error);
        Assert.Single(carried.Errors);
    }
}
