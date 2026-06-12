using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace MusicShare.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var settings = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<InternalApiSettings>>()
            .Value;

        if (string.IsNullOrWhiteSpace(settings.SharedSecret))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            return;
        }

        var providedSecret = context.HttpContext.Request.Headers[InternalApiSettings.SecretHeaderName].FirstOrDefault();

        if (!SecretsMatch(settings.SharedSecret, providedSecret))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    private static bool SecretsMatch(string expectedSecret, string? providedSecret)
    {
        if (string.IsNullOrEmpty(providedSecret))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
