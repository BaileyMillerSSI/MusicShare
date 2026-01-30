using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;
using MusicShare.Worker.Services;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumer that resolves songs on YouTube Music.
/// </summary>
public class YouTubeMusicLinkConsumer(
    MusicServiceResolver serviceResolver,
    ISongServiceLinkRepository linkRepository,
    ILogger<YouTubeMusicLinkConsumer> logger)
    : ServiceLinkConsumerBase(linkRepository, logger)
{
    protected override ServiceType ServiceType => ServiceType.YouTubeMusic;

    protected override IMusicServiceAdapter GetAdapter() =>
        serviceResolver.GetAdapter(ServiceType.YouTubeMusic)
        ?? throw new InvalidOperationException("YouTube Music adapter not registered");
}
