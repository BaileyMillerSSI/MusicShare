using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using MusicShare.Api.Security;

namespace MusicShare.Tests.Unit.Api.Security;

public sealed class MaintenanceApiKeyAttributeTests
{
    [Fact]
    public async Task ItWillRejectMissingAndInvalidMaintenanceSecrets()
    {
        var missing = await ExecuteAsync(string.Empty, null);
        missing.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);

        var invalid = await ExecuteAsync("configured-secret", "wrong-secret");
        invalid.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ItWillPermitOnlyTheConfiguredMaintenanceSecret()
    {
        var context = await ExecuteAsync("configured-secret", "configured-secret");
        context.Result.Should().BeNull();
        context.HttpContext.Items["continued"].Should().Be(true);
    }

    private static async Task<ActionExecutingContext> ExecuteAsync(string secret, string? supplied)
    {
        var options = new Mock<IOptionsMonitor<MaintenanceSettings>>();
        options.SetupGet(x => x.CurrentValue).Returns(new MaintenanceSettings { Secret = secret });
        var services = new ServiceCollection().AddSingleton(options.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        if (supplied is not null) http.Request.Headers["X-MAINTENANCE-KEY"] = supplied;
        var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), new object());
        await new MaintenanceApiKeyAttribute().OnActionExecutionAsync(context, () =>
        {
            http.Items["continued"] = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });
        return context;
    }
}
