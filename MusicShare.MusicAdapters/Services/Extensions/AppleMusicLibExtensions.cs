using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicShare.MusicAdapters.Services.Music.Apple;

namespace MusicShare.MusicAdapters.Services.Extensions
{
    public static class AppleMusicLibExtensions
    {
        public static TBuilder AddAppleMusicAccess<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Services.AddSingleton<IMusicServiceAdapter, AppleMusicMockAdapter>();

            return builder;
        }
    }
}
