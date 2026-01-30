using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicShare.MusicAdapters.Services.Music.YouTube;

namespace MusicShare.MusicAdapters.Services.Extensions
{
    public static class YouTubeMusicLibExtensions
    {
        public static TBuilder AddYouTubeMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Services.AddSingleton<IMusicServiceAdapter, YouTubeMusicMockAdapter>();

            return builder;
        }
    }
}
