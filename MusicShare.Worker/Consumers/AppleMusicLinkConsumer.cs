using MusicShare.Contracts;
using MusicShare.MusicAdapters;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumer that resolves songs on Apple Music.
/// </summary>
public class AppleMusicLinkConsumer(
    IMusicServiceResolver serviceResolver,
    ISongServiceLinkRepository linkRepository,
    ILogger<AppleMusicLinkConsumer> logger)
    : ServiceLinkConsumerBase(linkRepository, logger)
{
    protected override ServiceType ServiceType => ServiceType.AppleMusic;

    protected override IMusicServiceAdapter GetAdapter() =>
        serviceResolver.GetAdapter(ServiceType.AppleMusic)
        ?? throw new InvalidOperationException("Apple Music adapter not registered");
}
