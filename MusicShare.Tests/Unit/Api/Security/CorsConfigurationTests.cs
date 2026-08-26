using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MusicShare.Api.Security;

namespace MusicShare.Tests.Unit.Api.Security;

public class CorsConfigurationTests
{
    [Fact]
    public async Task ItWillUseExplicitProductionOriginsMethodsAndHeaders()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://music.baileymiller.dev/",
                ["Cors:AllowedOrigins:1"] = "https://resume.baileymiller.dev"
            })
            .Build();

        // Act
        var policyName = services.AddMusicShareCors(
            configuration,
            new TestHostEnvironment(Environments.Production));

        var provider = services.BuildServiceProvider().GetRequiredService<ICorsPolicyProvider>();
        var policy = await provider.GetPolicyAsync(new DefaultHttpContext(), policyName);

        // Assert
        policy.Should().NotBeNull();
        policy!.Origins.Should().BeEquivalentTo(
            "https://music.baileymiller.dev",
            "https://resume.baileymiller.dev");
        policy.IsOriginAllowed("https://music.baileymiller.dev").Should().BeTrue();
        policy.IsOriginAllowed("https://resume.baileymiller.dev").Should().BeTrue();
        policy.IsOriginAllowed("https://unrelated.example.com").Should().BeFalse();
        policy.Methods.Should().BeEquivalentTo("GET", "POST");
        policy.Headers.Should().BeEquivalentTo("Accept", "Content-Type");
        policy.AllowAnyOrigin.Should().BeFalse();
        policy.AllowAnyMethod.Should().BeFalse();
        policy.AllowAnyHeader.Should().BeFalse();
    }

    [Fact]
    public void ItWillRejectProductionCorsWithoutAllowedOrigins()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => services.AddMusicShareCors(
            configuration,
            new TestHostEnvironment(Environments.Production));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Production CORS requires at least one Cors:AllowedOrigins entry.");
    }

    [Fact]
    public async Task ItWillKeepDevelopmentCorsSeparateAndPermissive()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var policyName = services.AddMusicShareCors(
            configuration,
            new TestHostEnvironment(Environments.Development));

        var provider = services.BuildServiceProvider().GetRequiredService<ICorsPolicyProvider>();
        var policy = await provider.GetPolicyAsync(new DefaultHttpContext(), policyName);

        // Assert
        policyName.Should().Be(CorsConfiguration.DevelopmentPolicyName);
        policy.Should().NotBeNull();
        policy!.AllowAnyOrigin.Should().BeTrue();
        policy.AllowAnyMethod.Should().BeTrue();
        policy.AllowAnyHeader.Should().BeTrue();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "MusicShare.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
