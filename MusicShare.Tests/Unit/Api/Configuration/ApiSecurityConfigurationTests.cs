using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using MusicShare.Api.Configuration;

namespace MusicShare.Tests.Unit.Api.Configuration;

public class ApiSecurityConfigurationTests
{
    [Fact]
    public void ItWillBuildProductionCorsPolicyForConfiguredFrontendOrigins()
    {
        // Arrange
        var settings = new CorsSettings
        {
            AllowedOrigins = ["https://musicshare.example.com/"]
        };
        var policyBuilder = new CorsPolicyBuilder();

        // Act
        ApiSecurityConfiguration.ConfigureCorsPolicy(policyBuilder, settings, isDevelopment: false);
        var policy = policyBuilder.Build();

        // Assert
        policy.AllowAnyOrigin.Should().BeFalse();
        policy.AllowAnyMethod.Should().BeFalse();
        policy.AllowAnyHeader.Should().BeFalse();
        policy.Origins.Should().ContainSingle("https://musicshare.example.com");
        policy.Methods.Should().BeEquivalentTo("GET", "POST");
        policy.Headers.Should().BeEquivalentTo("Accept", "Content-Type");
    }

    [Fact]
    public void ItWillBuildDevelopmentCorsPolicyForLocalOriginsOnly()
    {
        // Arrange
        var policyBuilder = new CorsPolicyBuilder();

        // Act
        ApiSecurityConfiguration.ConfigureCorsPolicy(policyBuilder, new CorsSettings(), isDevelopment: true);
        var policy = policyBuilder.Build();

        // Assert
        policy.IsOriginAllowed("http://localhost:3000").Should().BeTrue();
        policy.IsOriginAllowed("http://127.0.0.1:5173").Should().BeTrue();
        policy.IsOriginAllowed("http://[::1]:3000").Should().BeTrue();
        policy.IsOriginAllowed("https://example.com").Should().BeFalse();
        policy.AllowAnyOrigin.Should().BeFalse();
        policy.AllowAnyMethod.Should().BeFalse();
        policy.AllowAnyHeader.Should().BeFalse();
    }

    [Fact]
    public void ItWillRejectProductionStartupWithoutAllowedOrigins()
    {
        // Arrange
        var environment = Mock.Of<IWebHostEnvironment>(x => x.EnvironmentName == "Production");

        // Act
        var act = () => ApiSecurityConfiguration.ValidateProductionSettings(
            new CorsSettings(),
            new InternalApiSettings { ApiKey = "internal-key" },
            environment);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Production CORS requires at least one Cors:AllowedOrigins value.");
    }

    [Fact]
    public void ItWillRejectProductionStartupWithoutInternalApiKey()
    {
        // Arrange
        var environment = Mock.Of<IWebHostEnvironment>(x => x.EnvironmentName == "Production");

        // Act
        var act = () => ApiSecurityConfiguration.ValidateProductionSettings(
            new CorsSettings { AllowedOrigins = ["https://musicshare.example.com"] },
            new InternalApiSettings(),
            environment);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Production internal APIs require InternalApi:ApiKey.");
    }
}
