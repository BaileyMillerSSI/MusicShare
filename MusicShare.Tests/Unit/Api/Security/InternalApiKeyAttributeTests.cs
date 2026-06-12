using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MusicShare.Api.Security;

namespace MusicShare.Tests.Unit.Api.Security;

public class InternalApiKeyAttributeTests
{
    [Fact]
    public void ItWillAuthorizeRequestsWithTheConfiguredInternalSecret()
    {
        // Arrange
        var context = CreateContext("expected-secret");
        context.HttpContext.Request.Headers[InternalApiSettings.SecretHeaderName] = "expected-secret";
        var sut = new InternalApiKeyAttribute();

        // Act
        sut.OnAuthorization(context);

        // Assert
        context.Result.Should().BeNull();
    }

    [Fact]
    public void ItWillRejectRequestsWithoutTheInternalSecretHeader()
    {
        // Arrange
        var context = CreateContext("expected-secret");
        var sut = new InternalApiKeyAttribute();

        // Act
        sut.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void ItWillRejectRequestsWithTheWrongInternalSecret()
    {
        // Arrange
        var context = CreateContext("expected-secret");
        context.HttpContext.Request.Headers[InternalApiSettings.SecretHeaderName] = "wrong-secret";
        var sut = new InternalApiKeyAttribute();

        // Act
        sut.OnAuthorization(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void ItWillFailClosedWhenTheInternalSecretIsNotConfigured()
    {
        // Arrange
        var context = CreateContext(null);
        context.HttpContext.Request.Headers[InternalApiSettings.SecretHeaderName] = "anything";
        var sut = new InternalApiKeyAttribute();

        // Act
        sut.OnAuthorization(context);

        // Assert
        var result = context.Result.Should().BeOfType<StatusCodeResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    private static AuthorizationFilterContext CreateContext(string? sharedSecret)
    {
        var services = new ServiceCollection()
            .Configure<InternalApiSettings>(settings => settings.SharedSecret = sharedSecret)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }
}
