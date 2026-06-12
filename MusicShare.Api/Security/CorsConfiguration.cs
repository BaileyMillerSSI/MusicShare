using Microsoft.Net.Http.Headers;

namespace MusicShare.Api.Security;

public static class CorsConfiguration
{
    public const string DevelopmentPolicyName = "MusicShareDevelopmentCors";
    public const string ProductionPolicyName = "MusicShareProductionCors";

    public static string AddMusicShareCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = GetAllowedOrigins(configuration);

        if (!environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Production CORS requires at least one Cors:AllowedOrigins entry.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(DevelopmentPolicyName, policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });

            options.AddPolicy(ProductionPolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST")
                    .WithHeaders(HeaderNames.Accept, HeaderNames.ContentType);
            });
        });

        return environment.IsDevelopment()
            ? DevelopmentPolicyName
            : ProductionPolicyName;
    }

    private static string[] GetAllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection(CorsSettings.SectionName)
            .Get<CorsSettings>()?
            .AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
}
