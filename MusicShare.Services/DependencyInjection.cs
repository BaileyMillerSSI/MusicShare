using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicShare.Services.Configuration;
using MusicShare.Services.Configuration.MusicServices;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
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
        builder
            .AddSpotifyAccess()
            .AddAppleMusicAccess()
            .AddYouTubeMusicAccess()
            .Services
            .AddSingleton<IMusicServiceResolver, MusicServiceResolver>();

        return builder;
    }

    private static TBuilder AddYouTubeMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services
            .AddOptions<YouTubeMusicConfiguration>()
            .Bind(builder.Configuration.GetSection(YouTubeMusicConfiguration.SectionName));

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

        builder
            .Services
            .AddHttpClient<IMusicServiceAdapter, SpotifyMusicService>(config =>
            {
                config.BaseAddress = new Uri("https://api.spotify.com/v1/");
            })
            .AddHttpMessageHandler<SpotifyAccessTokenHandler>();

        builder
            .Services
            .AddScoped<SpotifyAccessTokenHandler>();

        return builder;
    }

    private static TBuilder AddAppleMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // TODO: implement an apple music adapter
        //builder.Services.AddSingleton<IMusicServiceAdapter, AppleMusicMockAdapter>();

        return builder;
    }
}
