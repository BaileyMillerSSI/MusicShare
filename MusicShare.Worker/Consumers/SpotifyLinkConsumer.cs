using MusicShare.Contracts;
using MusicShare.MusicAdapters;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumer that resolves songs on Spotify.
/// </summary>
public class SpotifyLinkConsumer(
    IMusicServiceResolver serviceResolver,
    ISongServiceLinkRepository linkRepository,
    ILogger<SpotifyLinkConsumer> logger)
    : ServiceLinkConsumerBase(linkRepository, logger)
{
    protected override ServiceType ServiceType => ServiceType.Spotify;

    protected override IMusicServiceAdapter GetAdapter() =>
        serviceResolver.GetAdapter(ServiceType.Spotify)
        ?? throw new InvalidOperationException("Spotify adapter not registered");
}
