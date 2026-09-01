using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace MusicShare.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MaintenanceApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var settings = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<MaintenanceSettings>>().CurrentValue;
        if (string.IsNullOrEmpty(settings.Secret))
        {
            context.Result = new ObjectResult(new { error = "Maintenance is not configured" }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return Task.CompletedTask;
        }
        var supplied = context.HttpContext.Request.Headers["X-MAINTENANCE-KEY"].ToString();
        var valid = supplied.Length == settings.Secret.Length &&
                    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.Secret));
        if (!valid)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }
        return next();
    }
}
