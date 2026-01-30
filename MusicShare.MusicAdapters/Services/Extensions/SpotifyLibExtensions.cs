using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicShare.MusicAdapters.Configuration.MusicServices;
using MusicShare.MusicAdapters.Services.Music.Spotify;
using System.Text;

namespace MusicShare.MusicAdapters.Services.Extensions
{
    public static class SpotifyLibExtensions
    {
        public static TBuilder AddSpotifyAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
    }
}
