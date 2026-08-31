using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MusicShare.Services;
using MusicShare.Services.Configuration;
using MusicShare.Services.Services;

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
        });

        builder.AddFrontendRevalidateService();
        using var host = builder.Build();

        // Act
        var options = host.Services.GetRequiredService<IOptions<FrontendSettings>>();

        // Assert
        options.Value.RevalidationSecret.Should().Be("my-secret-key");
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
        await service.RevalidateShareAsync("test-share");

        // Assert
        capturedBaseAddress.Should().NotBeNull();
        capturedBaseAddress!.GetLeftPart(UriPartial.Authority)
            .Should().Be("https+http://frontend");
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
        await service.RevalidateShareAsync("test-share");

        // Assert
        capturedApiKey.Should().Be("super-secret-api-key");
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
