using LibNetCore.Core.Primitives;

namespace LibNetCore.Core.Tests.Primitives;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_AndCarriesNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_IsFailure_AndCarriesTheError()
    {
        var error = Error.NotFound("User.NotFound", "Không tìm thấy.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    // Thất bại mà không nói được vì sao là một lỗi lập trình, không phải
    // một trạng thái hợp lệ. Chặn ngay lúc tạo, đừng để nó đi tiếp.
    [Fact]
    public void Failure_WithNoError_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void GenericSuccess_ExposesTheValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    // Đọc giá trị của một kết quả thất bại là hỏi sai câu hỏi.
    // Nếu trả về null hoặc default thì lỗi sẽ trôi đi và nổ ở chỗ khác.
    [Fact]
    public void GenericFailure_ReadingValue_Throws()
    {
        var result = Result.Failure<int>(Error.Failure("Db.Down", "Mất kết nối."));

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void GenericSuccess_WithNullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    // Hai phép chuyển ngầm này để handler viết "return user;" và "return Error.NotFound(...);"
    // thay vì lúc nào cũng phải gõ Result.Success(...) / Result.Failure(...).
    [Fact]
    public void ImplicitConversion_FromValue_GivesSuccess()
    {
        Result<string> result = "xin chào";

        Assert.True(result.IsSuccess);
        Assert.Equal("xin chào", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_GivesFailure()
    {
        var error = Error.Conflict("User.EmailTaken", "Email đã dùng.");

        Result<string> result = error;

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }
}
