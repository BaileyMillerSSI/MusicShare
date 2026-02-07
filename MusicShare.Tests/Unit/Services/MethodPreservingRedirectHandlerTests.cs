using System.Net;
using System.Net.Http.Json;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class MethodPreservingRedirectHandlerTests
{
    private static HttpClient CreateClient(
        MethodPreservingRedirectHandler handler,
        HttpMessageHandler innerHandler)
    {
        handler.InnerHandler = innerHandler;
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://frontend.local")
        };
    }

    [Fact]
    public async Task ItWillPassThroughNonRedirectResponses()
    {
        var callCount = 0;
        var inner = new MockHttpMessageHandler((_, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        var response = await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task ItWillPreservePostMethodOnRedirect(HttpStatusCode redirectCode)
    {
        HttpMethod? retryMethod = null;
        var callCount = 0;
        var inner = new MockHttpMessageHandler((request, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var redirect = new HttpResponseMessage(redirectCode);
                redirect.Headers.Location = new Uri("https://frontend.prod/api/revalidate");
                return Task.FromResult(redirect);
            }

            retryMethod = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        retryMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task ItWillSendRetryToRedirectLocation()
    {
        Uri? retryUri = null;
        var callCount = 0;
        var inner = new MockHttpMessageHandler((request, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("https://frontend.prod/api/revalidate");
                return Task.FromResult(redirect);
            }

            retryUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        retryUri.Should().Be(new Uri("https://frontend.prod/api/revalidate"));
    }

    [Fact]
    public async Task ItWillHandleRelativeLocationHeader()
    {
        Uri? retryUri = null;
        var callCount = 0;
        var inner = new MockHttpMessageHandler((request, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("/api/revalidate", UriKind.Relative);
                return Task.FromResult(redirect);
            }

            retryUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        retryUri.Should().NotBeNull();
        retryUri!.AbsolutePath.Should().Be("/api/revalidate");
    }

    [Fact]
    public async Task ItWillPreserveRequestContentOnRedirect()
    {
        string? retryBody = null;
        var callCount = 0;
        var inner = new MockHttpMessageHandler(async (request, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("https://frontend.prod/api/revalidate");
                return redirect;
            }

            retryBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        retryBody.Should().NotBeNull();
        retryBody.Should().Contain("abc");
    }

    [Fact]
    public async Task ItWillPreserveRequestHeadersOnRedirect()
    {
        string? apiKeyHeader = null;
        var callCount = 0;
        var inner = new MockHttpMessageHandler((request, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("https://frontend.prod/api/revalidate");
                return Task.FromResult(redirect);
            }

            apiKeyHeader = request.Headers.TryGetValues("X-API-KEY", out var values)
                ? string.Join(",", values)
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/revalidate")
        {
            Content = JsonContent.Create(new { ShareId = "abc" })
        };
        request.Headers.TryAddWithoutValidation("X-API-KEY", "test-secret");

        await client.SendAsync(request, TestContext.Current.CancellationToken);

        apiKeyHeader.Should().Be("test-secret");
    }

    [Fact]
    public async Task ItWillCacheBaseAddressForSubsequentRequests()
    {
        var requestUris = new List<Uri?>();
        var callCount = 0;
        var inner = new MockHttpMessageHandler((request, _) =>
        {
            callCount++;
            requestUris.Add(request.RequestUri);

            if (callCount == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("https://frontend.prod/api/revalidate");
                return Task.FromResult(redirect);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new MethodPreservingRedirectHandler();
        var client = CreateClient(handler, inner);

        // First request triggers redirect and caches
        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "first" }, cancellationToken: TestContext.Current.CancellationToken);

        // Second request should use cached HTTPS base directly
        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "second" }, cancellationToken: TestContext.Current.CancellationToken);

        // Call 1: original HTTP, Call 2: retry to HTTPS, Call 3: second request using cached HTTPS
        callCount.Should().Be(3);
        requestUris[2]!.Scheme.Should().Be("https");
        requestUris[2]!.Host.Should().Be("frontend.prod");
    }

    [Fact]
    public async Task ItWillReturnRedirectWhenLocationHeaderMissing()
    {
        var inner = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.MovedPermanently)));
        var client = CreateClient(new MethodPreservingRedirectHandler(), inner);

        var response = await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "abc" }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
    }

    [Fact]
    public async Task ItWillNotCacheOnNonRedirectResponse()
    {
        var requestUris = new List<Uri?>();
        var inner = new MockHttpMessageHandler((request, _) =>
        {
            requestUris.Add(request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new MethodPreservingRedirectHandler();
        var client = CreateClient(handler, inner);

        // Two successful requests — both should use original HTTP base
        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "first" }, cancellationToken: TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync("/api/revalidate", new { ShareId = "second" }, cancellationToken: TestContext.Current.CancellationToken);

        requestUris.Should().HaveCount(2);
        requestUris[0]!.Scheme.Should().Be("http");
        requestUris[1]!.Scheme.Should().Be("http");
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
