using System.Net;
using Microsoft.Extensions.Logging;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class FrontendRevalidateServiceTests
{
    private static FrontendRevalidateService CreateSut(
        HttpMessageHandler handler,
        ILogger<FrontendRevalidateService>? logger = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://frontend.example.com")
        };
        return new FrontendRevalidateService(
            client,
            logger ?? Mock.Of<ILogger<FrontendRevalidateService>>());
    }

    [Fact]
    public async Task ItWillPostToRevalidateEndpoint()
    {
        // Arrange
        Uri? capturedUri = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sut = CreateSut(handler);
        var request = new RevalidateFrontendRequest("share-abc123");

        // Act
        await sut.RevalidateAsync(request);

        // Assert
        capturedUri.Should().NotBeNull();
        capturedUri!.PathAndQuery.Should().Be("/api/revalidate");
    }

    [Fact]
    public async Task ItWillSendPostRequest()
    {
        // Arrange
        HttpMethod? capturedMethod = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedMethod = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sut = CreateSut(handler);

        // Act
        await sut.RevalidateAsync(new RevalidateFrontendRequest("share-1"));

        // Assert
        capturedMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task ItWillIncludeShareIdInRequestBody()
    {
        // Arrange
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sut = CreateSut(handler);

        // Act
        await sut.RevalidateAsync(new RevalidateFrontendRequest("share-abc123"));

        // Assert
        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("share-abc123");
    }

    [Fact]
    public async Task ItWillNotThrowOnHttpFailure()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.RevalidateAsync(new RevalidateFrontendRequest("share-1"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItWillNotThrowOnNetworkException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection refused"));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.RevalidateAsync(new RevalidateFrontendRequest("share-1"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItWillNotThrowOnTaskCanceledException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("Request timed out"));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.RevalidateAsync(new RevalidateFrontendRequest("share-1"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItWillCompleteSuccessfullyOnOkResponse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.RevalidateAsync(new RevalidateFrontendRequest("share-abc123"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItWillNotThrowOnBadRequestResponse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.RevalidateAsync(new RevalidateFrontendRequest("share-1"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItWillNotThrowOnServiceUnavailableResponse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.RevalidateAsync(new RevalidateFrontendRequest("share-1"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Test helper that intercepts HTTP requests for assertion.
    /// </summary>
    private class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
