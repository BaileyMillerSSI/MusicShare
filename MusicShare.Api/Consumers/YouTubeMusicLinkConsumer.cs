using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;

namespace MusicShare.Api.Consumers;

/// <summary>
/// Consumer that resolves songs on YouTube Music.
/// </summary>
public class YouTubeMusicLinkConsumer(
    IMusicServiceResolver serviceResolver,
    ISongServiceLinkRepository linkRepository,
    ILogger<YouTubeMusicLinkConsumer> logger)
    : ServiceLinkConsumerBase(linkRepository, logger)
{
    protected override ServiceType ServiceType => ServiceType.YouTubeMusic;

    protected override IMusicServiceAdapter GetAdapter() =>
        serviceResolver.GetAdapter(ServiceType.YouTubeMusic)
        ?? throw new InvalidOperationException("YouTube Music adapter not registered");
}
