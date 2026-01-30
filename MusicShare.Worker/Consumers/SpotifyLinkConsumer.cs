using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;
using MusicShare.Worker.Services;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumer that resolves songs on Spotify.
/// </summary>
public class SpotifyLinkConsumer(
    MusicServiceResolver serviceResolver,
    ISongServiceLinkRepository linkRepository,
    ILogger<SpotifyLinkConsumer> logger)
    : ServiceLinkConsumerBase(linkRepository, logger)
{
    protected override ServiceType ServiceType => ServiceType.Spotify;

    protected override IMusicServiceAdapter GetAdapter() =>
        serviceResolver.GetAdapter(ServiceType.Spotify)
        ?? throw new InvalidOperationException("Spotify adapter not registered");
}
