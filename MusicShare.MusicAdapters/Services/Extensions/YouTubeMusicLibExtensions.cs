using MassTransit.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicShare.MusicAdapters.Configuration.MusicServices;
using MusicShare.MusicAdapters.Services.Music.YouTube;
using YouTubeMusicAPI.Client;

namespace MusicShare.MusicAdapters.Services.Extensions
{
    public static class YouTubeMusicLibExtensions
    {
        public static TBuilder AddYouTubeMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
    }
}
