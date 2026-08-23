using Luong.Kernel.Primitives;

namespace Luong.Kernel.Tests.Primitives;

public class ErrorTests
{
    [Fact]
    public void None_HasEmptyCode()
    {
        Assert.Equal(string.Empty, Error.None.Code);
    }

    [Fact]
    public void Validation_SetsValidationType()
    {
        var error = Error.Validation("User.EmailInvalid", "Email không hợp lệ.");

        Assert.Equal("User.EmailInvalid", error.Code);
        Assert.Equal("Email không hợp lệ.", error.Description);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void NotFound_SetsNotFoundType()
    {
        Assert.Equal(ErrorType.NotFound, Error.NotFound("User.NotFound", "Không tìm thấy.").Type);
    }

    [Fact]
    public void Conflict_SetsConflictType()
    {
        Assert.Equal(ErrorType.Conflict, Error.Conflict("User.EmailTaken", "Email đã dùng.").Type);
    }

    [Fact]
    public void Unauthorized_SetsUnauthorizedType()
    {
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("Auth.Invalid", "Sai thông tin.").Type);
    }

    [Fact]
    public void Forbidden_SetsForbiddenType()
    {
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("Org.NotYourDept", "Không thuộc phòng.").Type);
    }

    [Fact]
    public void Failure_SetsFailureType()
    {
        Assert.Equal(ErrorType.Failure, Error.Failure("Db.Down", "Mất kết nối.").Type);
    }

    // Hai lỗi cùng nội dung phải BẰNG NHAU. Không có tính chất này thì
    // service dùng lib sẽ không so sánh lỗi trong test được.
    [Fact]
    public void TwoErrors_WithSameContent_AreEqual()
    {
        var a = Error.NotFound("User.NotFound", "Không tìm thấy.");
        var b = Error.NotFound("User.NotFound", "Không tìm thấy.");

        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoErrors_WithDifferentCode_AreNotEqual()
    {
        var a = Error.NotFound("User.NotFound", "Không tìm thấy.");
        var b = Error.NotFound("Dept.NotFound", "Không tìm thấy.");

        Assert.NotEqual(a, b);
    }
}
