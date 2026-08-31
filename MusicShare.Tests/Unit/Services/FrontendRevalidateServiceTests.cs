using System.Net;
using Microsoft.Extensions.Logging;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class FrontendRevalidateServiceTests
{
    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private static FrontendRevalidateService CreateSut(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://frontend.example.com") },
        Mock.Of<ILogger<FrontendRevalidateService>>());

    [Fact]
    public async Task ItWillPostTheExactSharePayload()
    {
        string? body = null;
        var sut = CreateSut(new MockHttpMessageHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        await sut.RevalidateShareAsync("abc123def456");
        body.Should().Be("{\"shareId\":\"abc123def456\"}");
    }

    [Fact]
    public async Task ItWillPostTheFixedMetricsPayload()
    {
        string? body = null;
        var sut = CreateSut(new MockHttpMessageHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        (await sut.RevalidateMetricsAsync()).Should().BeTrue();
        body.Should().Be("{\"target\":\"metrics\"}");
    }

    [Fact]
    public async Task ItWillTolerateFrontendFailure()
    {
        var sut = CreateSut(new MockHttpMessageHandler((_, _) => throw new HttpRequestException("offline")));
        var result = await sut.RevalidateMetricsAsync();
        result.Should().BeFalse();
    }
}
