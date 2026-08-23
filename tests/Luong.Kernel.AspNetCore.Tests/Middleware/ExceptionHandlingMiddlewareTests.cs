using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Luong.Kernel.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Luong.Kernel.AspNetCore.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private const string SecretMessage = "Kết nối DB thất bại: Host=10.0.0.5;Password=hunter2";

    private static async Task<IHost> StartHostAsync(RequestDelegate endpoint)
    {
        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddLogging())
                .Configure(app =>
                {
                    app.UseCorrelationId();
                    app.UseProblemDetailsExceptionHandler();
                    app.Run(endpoint);
                }))
            .StartAsync();
    }

    [Fact]
    public async Task UnhandledException_Returns500()
    {
        using var host = await StartHostAsync(_ => throw new InvalidOperationException(SecretMessage));

        var response = await host.GetTestClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_RespondsAsProblemJson()
    {
        using var host = await StartHostAsync(_ => throw new InvalidOperationException(SecretMessage));

        var response = await host.GetTestClient().GetAsync("/");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Điểm quan trọng nhất của middleware này. Thông báo exception thường chứa
    // chuỗi kết nối, tên máy chủ, mật khẩu. Đẩy ra ngoài là tặng bản đồ cho kẻ tấn công.
    [Fact]
    public async Task UnhandledException_DoesNotLeakInternalDetails()
    {
        using var host = await StartHostAsync(_ => throw new InvalidOperationException(SecretMessage));

        var response = await host.GetTestClient().GetAsync("/");
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("10.0.0.5", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
    }

    // Đổi lại, phải trả về một mã để người dùng đọc cho tổng đài -
    // rồi từ mã đó lần ra đúng dòng log chứa stack trace thật.
    [Fact]
    public async Task UnhandledException_ReturnsCorrelationIdSoTheLogCanBeFound()
    {
        using var host = await StartHostAsync(_ => throw new InvalidOperationException(SecretMessage));

        var response = await host.GetTestClient().GetAsync("/");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        string correlationId = problem.GetProperty("correlationId").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-Id").Single());
    }
}

public class CorrelationIdMiddlewareTests
{
    private static async Task<IHost> StartHostAsync()
    {
        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddLogging())
                .Configure(app =>
                {
                    app.UseCorrelationId();
                    app.Run(ctx => ctx.Response.WriteAsync("ok"));
                }))
            .StartAsync();
    }

    [Fact]
    public async Task EveryResponse_CarriesACorrelationId()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("X-Correlation-Id").Single()));
    }

    // Khi gateway hoặc service khác đã gắn mã rồi thì phải GIỮ NGUYÊN,
    // không sinh mã mới - nếu không thì một request đi qua 3 service sẽ có 3 mã
    // khác nhau và mất hẳn khả năng lần vết.
    [Fact]
    public async Task IncomingCorrelationId_IsKept()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "mã-từ-gateway-123");

        var response = await client.GetAsync("/");

        Assert.Equal("mã-từ-gateway-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }
}
