using Luong.Kernel.AspNetCore.Results;
using Luong.Kernel.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Luong.Kernel.AspNetCore.Tests.Results;

public class ResultHttpExtensionsTests
{
    private static async Task<(int Status, string Body)> RunAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    // Thao tác không trả dữ liệu (xoá, đổi phòng ban) mà thành công thì 204,
    // không phải 200 kèm thân rỗng.
    [Fact]
    public async Task Success_WithoutValue_Returns204()
    {
        var (status, body) = await RunAsync(Result.Success().ToHttpResult());

        Assert.Equal(204, status);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Success_WithValue_Returns200AndTheValue()
    {
        var (status, body) = await RunAsync(Result.Success(new { Name = "Lê Anh Lượng" }).ToHttpResult());

        Assert.Equal(200, status);
        Assert.Contains("Lê Anh Lượng", body);
    }

    [Fact]
    public async Task Failure_UsesTheStatusCodeOfTheErrorType()
    {
        var error = Error.NotFound("Dept.NotFound", "Không tìm thấy phòng ban.");

        var (status, body) = await RunAsync(Result.Failure<string>(error).ToHttpResult());

        Assert.Equal(404, status);
        Assert.Contains("Dept.NotFound", body);
    }

    [Fact]
    public async Task Failure_WithManyValidationErrors_ListsThemAll()
    {
        var error = new ValidationError(
        [
            Error.Validation("User.EmailInvalid", "Email không hợp lệ."),
            Error.Validation("User.PhoneInvalid", "Số điện thoại không hợp lệ."),
        ]);

        var (status, body) = await RunAsync(Result.Failure(error).ToHttpResult());

        Assert.Equal(400, status);
        Assert.Contains("User.EmailInvalid", body);
        Assert.Contains("User.PhoneInvalid", body);
    }
}
