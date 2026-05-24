using System.Net;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace MusicShare.Api.Configuration;

public static class ApiSecurityConfiguration
{
    private static readonly string[] AllowedMethods = ["GET", "POST"];
    private static readonly string[] AllowedHeaders = [HeaderNames.Accept, HeaderNames.ContentType];
    private const string DevelopmentInternalApiKey = "local-dev-internal-api-key";

    public static IServiceCollection AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
        var internalApiSettings = configuration.GetSection(InternalApiSettings.SectionName).Get<InternalApiSettings>() ?? new InternalApiSettings();

        ValidateProductionSettings(corsSettings, internalApiSettings, environment);

        services.Configure<InternalApiSettings>(configuration.GetSection(InternalApiSettings.SectionName));
        if (environment.IsDevelopment())
        {
            services.PostConfigure<InternalApiSettings>(settings =>
            {
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    settings.ApiKey = DevelopmentInternalApiKey;
                }
            });
        }

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => ConfigureCorsPolicy(policy, corsSettings, environment.IsDevelopment()));
        });

        return services;
    }

    public static void ConfigureCorsPolicy(
        CorsPolicyBuilder policy,
        CorsSettings settings,
        bool isDevelopment)
    {
        policy
            .WithMethods(AllowedMethods)
            .WithHeaders(AllowedHeaders);

        if (isDevelopment)
        {
            policy.SetIsOriginAllowed(IsLocalDevelopmentOrigin);
            return;
        }

        policy.WithOrigins(NormalizeOrigins(settings.AllowedOrigins));
    }

    public static void ValidateProductionSettings(
        CorsSettings corsSettings,
        InternalApiSettings internalApiSettings,
        IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        if (NormalizeOrigins(corsSettings.AllowedOrigins).Length == 0)
        {
            throw new InvalidOperationException("Production CORS requires at least one Cors:AllowedOrigins value.");
        }

        if (string.IsNullOrWhiteSpace(internalApiSettings.ApiKey))
        {
            throw new InvalidOperationException("Production internal APIs require InternalApi:ApiKey.");
        }
    }

    private static bool IsLocalDevelopmentOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    private static string[] NormalizeOrigins(IEnumerable<string> origins)
    {
        return origins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
