using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicShare.Services.Configuration;
using MusicShare.Services.Configuration.MusicServices;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
using MusicShare.Services.Services.Music.Apple;
using MusicShare.Services.Services.Music.Spotify;
using MusicShare.Services.Services.Music.YouTube;
using YouTubeMusicAPI.Client;

namespace MusicShare.Services;

public static class DependencyInjection
{
    public static TBuilder AddDomainServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddScoped<ISongService, SongService>();
        builder.Services.AddScoped<IShareStatusService, ShareStatusService>();
        builder.Services.AddScoped<IShareRequestService, ShareRequestService>();

        return builder
            .AddMusicServices()
            .AddFrontendRevalidateService();
    }

    public static TBuilder AddFrontendRevalidateService<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services
            .AddOptions<FrontendSettings>()
            .Bind(builder.Configuration.GetSection(FrontendSettings.SectionName));

        builder.Services.AddTransient<MethodPreservingRedirectHandler>();

        builder.Services.AddHttpClient<IFrontendRevalidateService, FrontendRevalidateService>((svp, client) =>
        {
            var settings = svp.GetRequiredService<IOptionsMonitor<FrontendSettings>>();

            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-KEY", settings.CurrentValue.RevalidationSecret);
            client.BaseAddress = FrontendSettings.Uri;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        .AddHttpMessageHandler<MethodPreservingRedirectHandler>();

        return builder;
    }

    private static TBuilder AddMusicServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Register the confidence scoring service (used by ConfidenceAdapter)
        builder.Services.AddSingleton<IConfidenceScoreService, ConfidenceScoreService>();

        // Register all music service implementations and adapters
        builder
            .AddSpotifyAccess()
            .AddAppleMusicAccess()
            .AddYouTubeMusicAccess()
            .Services
            .AddSingleton<IMusicServiceResolver, MusicServiceResolver>();

        // Decorate ALL IMusicServiceAdapter registrations with ConfidenceAdapter
        // This single line wraps each adapter (Spotify, Apple, YouTube) with confidence filtering
        // Using Scrutor's Decorate method - automatically resolves ILogger<ConfidenceAdapter> and IConfidenceScoreService
        builder.Services.Decorate<IMusicServiceAdapter>(
            (inner, provider) => new ConfidenceAdapter(
                inner,
                provider.GetRequiredService<IConfidenceScoreService>(),
                provider.GetRequiredService<ILogger<ConfidenceAdapter>>()
            )
        );

        return builder;
    }

    private static TBuilder AddYouTubeMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services
            .AddOptions<YouTubeMusicConfiguration>()
            .Bind(builder.Configuration.GetSection(YouTubeMusicConfiguration.SectionName));

        // Register YouTube Music service
        builder.Services.AddTransient<IYouTubeMusicService, YouTubeMusicService>();

        // Register YouTube Music adapter
        builder.Services.AddTransient<IMusicServiceAdapter, YouTubeMusicAdapter>();

        builder.Services.AddHttpClient(nameof(YouTubeMusicClient), client =>
        {
            // TODO: Configure HttpClient if needed
        });

        builder.Services.AddTransient(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(nameof(YouTubeMusicClient));

            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpFactory.CreateClient(nameof(YouTubeMusicClient));

            var config = sp.GetRequiredService<IOptions<YouTubeMusicConfiguration>>().Value;

            return new YouTubeMusicClient(
                logger: logger,
                geographicalLocation: config.GeographicLocation,
                httpClient: httpClient);
        });

        return builder;
    }

    private static TBuilder AddSpotifyAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder
            .Services
            .AddOptions<SpotifyConfiguration>()
            .Bind(builder.Configuration.GetSection(SpotifyConfiguration.SectionName));

        // Register Spotify service
        builder
            .Services
            .AddHttpClient<ISpotifyMusicService, SpotifyMusicServiceImpl>(config =>
            {
                config.BaseAddress = new Uri("https://api.spotify.com/v1/");
            })
            .AddHttpMessageHandler<SpotifyAccessTokenHandler>();

        // Register Spotify adapter
        builder.Services.AddTransient<IMusicServiceAdapter, SpotifyMusicAdapter>();

        builder
            .Services
            .AddScoped<SpotifyAccessTokenHandler>();

        return builder;
    }

    private static TBuilder AddAppleMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Register Apple Music service (mock implementation)
        builder.Services.AddTransient<IAppleMusicService, AppleMusicService>();

        // Register Apple Music adapter (mock implementation)
        builder.Services.AddTransient<IMusicServiceAdapter, AppleMusicMockAdapter>();

        return builder;
    }
}
