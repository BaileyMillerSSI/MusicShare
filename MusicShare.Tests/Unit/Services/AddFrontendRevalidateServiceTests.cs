using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MusicShare.Services;
using MusicShare.Services.Configuration;
using MusicShare.Worker.Services;

namespace MusicShare.Tests.Unit.Services;

public class AddFrontendRevalidateServiceTests
{
    [Fact]
    public void ItWillRegisterFrontendRevalidateService()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "test-secret",
            ["services:frontend:https:0"] = "https://frontend.example.com",
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFrontendRevalidateService>();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<FrontendRevalidateService>();
    }

    [Fact]
    public void ItWillBindFrontendSettingsFromConfiguration()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "my-secret-key",
            ["services:frontend:https:0"] = "https://frontend.example.com",
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        var options = host.Services.GetRequiredService<IOptions<FrontendSettings>>();

        // Assert
        options.Value.RevalidationSecret.Should().Be("my-secret-key");
    }

    [Fact]
    public void ItWillConfigureAspireServiceUrls()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "secret",
            ["services:frontend:https:0"] = "https://frontend.example.com",
            ["services:frontend:http:0"] = "http://frontend.example.com",
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        var options = host.Services.GetRequiredService<IOptions<FrontendSettings>>();

        // Assert
        options.Value.UrlHttps.Should().Be("https://frontend.example.com");
        options.Value.UrlHttp.Should().Be("http://frontend.example.com");
    }

    [Fact]
    public async Task ItWillConfigureHttpClientBaseAddress()
    {
        // Arrange
        var capturedBaseAddress = default(Uri);
        var handler = new CapturingDelegatingHandler(request =>
        {
            capturedBaseAddress = request.RequestUri;
        });

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "test-secret",
            ["services:frontend:https:0"] = "https://frontend.example.com",
        });

        builder.AddFrontendRevalidateService();
        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddHttpMessageHandler(() => handler);
        });

        using var host = builder.Build();

        // Act
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFrontendRevalidateService>();

        // Trigger an actual HTTP call to capture the configured base address
        // The handler will capture the request and short-circuit it
        await service.RevalidateAsync(new RevalidateFrontendRequest("test-share"));

        // Assert
        capturedBaseAddress.Should().NotBeNull();
        capturedBaseAddress!.GetLeftPart(UriPartial.Authority)
            .Should().Be("https://frontend.example.com");
    }

    [Fact]
    public async Task ItWillConfigureApiKeyHeader()
    {
        // Arrange
        string? capturedApiKey = null;
        var handler = new CapturingDelegatingHandler(request =>
        {
            if (request.Headers.TryGetValues("X-API-KEY", out var values))
            {
                capturedApiKey = values.FirstOrDefault();
            }
        });

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "super-secret-api-key",
            ["services:frontend:https:0"] = "https://frontend.example.com",
        });

        builder.AddFrontendRevalidateService();
        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddHttpMessageHandler(() => handler);
        });

        using var host = builder.Build();

        // Act
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFrontendRevalidateService>();
        await service.RevalidateAsync(new RevalidateFrontendRequest("test-share"));

        // Assert
        capturedApiKey.Should().Be("super-secret-api-key");
    }

    [Fact]
    public void ItWillPreferHttpsUrlOverHttp()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "secret",
            ["services:frontend:https:0"] = "https://secure.example.com",
            ["services:frontend:http:0"] = "http://insecure.example.com",
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        var options = host.Services.GetRequiredService<IOptions<FrontendSettings>>();

        // Assert
        options.Value.Uri.Should().Be(new Uri("https://secure.example.com"));
    }

    [Fact]
    public void ItWillFallBackToHttpUrlWhenHttpsNotConfigured()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "secret",
            ["services:frontend:http:0"] = "http://fallback.example.com",
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        var options = host.Services.GetRequiredService<IOptions<FrontendSettings>>();

        // Assert
        options.Value.Uri.Should().Be(new Uri("http://fallback.example.com"));
    }

    [Fact]
    public void ItWillDefaultAspireUrlsToEmptyStringWhenNotConfigured()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:RevalidationSecret"] = "secret",
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        var options = host.Services.GetRequiredService<IOptions<FrontendSettings>>();

        // Assert
        options.Value.UrlHttps.Should().BeEmpty();
        options.Value.UrlHttp.Should().BeEmpty();
    }

    /// <summary>
    /// A test delegating handler that captures the outgoing request for inspection
    /// and returns a successful response without making a real HTTP call.
    /// </summary>
    private sealed class CapturingDelegatingHandler(Action<HttpRequestMessage> onRequest) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
