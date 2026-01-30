using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MusicShare.MusicAdapters.Services.Extensions
{
    public static class MusicServiceLibExtensions
    {
        public static TBuilder AddMusicServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder
                .AddSpotifyAccess()
                .AddAppleMusicAccess()
                .AddYouTubeMusicAccess()
                .Services
                .AddSingleton<IMusicServiceResolver, MusicServiceResolver>();

            return builder;

        }
    }
}
