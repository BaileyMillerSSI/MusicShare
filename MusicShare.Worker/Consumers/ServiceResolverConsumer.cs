using MassTransit;
using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Worker.Services;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumes SongMetadataResolved messages and attempts to find the song on other music services.
/// This consumer is idempotent and retry-safe.
/// </summary>
public class ServiceResolverConsumer : IConsumer<SongMetadataResolved>
{
    private readonly ILogger<ServiceResolverConsumer> _logger;
    private readonly MusicServiceResolver _serviceResolver;
    private readonly ISongRepository _songRepository;
    private readonly ISongServiceLinkRepository _linkRepository;
    private readonly IShareRequestRepository _shareRequestRepository;

    public ServiceResolverConsumer(
        ILogger<ServiceResolverConsumer> logger,
        MusicServiceResolver serviceResolver,
        ISongRepository songRepository,
        ISongServiceLinkRepository linkRepository,
        IShareRequestRepository shareRequestRepository)
    {
        _logger = logger;
        _serviceResolver = serviceResolver;
        _songRepository = songRepository;
        _linkRepository = linkRepository;
        _shareRequestRepository = shareRequestRepository;
    }

    public async Task Consume(ConsumeContext<SongMetadataResolved> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing SongMetadataResolved: SongId={SongId}, Title={Title}",
            message.SongId, message.Metadata.Title);

        try
        {
            // Get the song to determine which service it came from
            var song = await _songRepository.GetByIdAsync(message.SongId, context.CancellationToken);
            if (song == null)
            {
                _logger.LogWarning("Song not found: SongId={SongId}", message.SongId);
                return;
            }

            // Get existing links to determine source service
            var existingLinks = await _linkRepository.GetBySongIdAsync(message.SongId, context.CancellationToken);
            var sourceServiceType = existingLinks.FirstOrDefault()?.ServiceType ?? ServiceType.Unknown;

            // Get all adapters except the source service
            var otherAdapters = _serviceResolver.GetOtherAdapters(sourceServiceType).ToList();

            _logger.LogInformation(
                "Searching for song on {Count} other services (excluding {SourceService})",
                otherAdapters.Count, sourceServiceType);

            var resolvedCount = 0;

            // Attempt to find the song on each other service
            foreach (var adapter in otherAdapters)
            {
                try
                {
                    // Check if we already have a link for this service (idempotent check)
                    var existingLink = await _linkRepository.GetBySongIdAndServiceAsync(
                        message.SongId, adapter.ServiceType, context.CancellationToken);

                    if (existingLink != null)
                    {
                        _logger.LogInformation(
                            "Link already exists for SongId={SongId}, ServiceType={ServiceType}",
                            message.SongId, adapter.ServiceType);
                        resolvedCount++;
                        continue;
                    }

                    // Try to find the song on this service
                    var foundUrl = await adapter.FindSongAsync(message.Metadata, context.CancellationToken);

                    if (!string.IsNullOrEmpty(foundUrl))
                    {
                        var normalizedUrl = adapter.NormalizeUrl(foundUrl);
                        var serviceSongId = adapter.ExtractSongId(foundUrl) ?? foundUrl;

                        var link = new SongServiceLink
                        {
                            SongId = message.SongId,
                            ServiceType = adapter.ServiceType,
                            ServiceSongId = serviceSongId,
                            OriginalUrl = foundUrl,
                            NormalizedUrl = normalizedUrl,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _linkRepository.InsertAsync(link, context.CancellationToken);

                        // Publish ServiceLinkResolved event
                        await context.Publish(new ServiceLinkResolved
                        {
                            SongId = message.SongId,
                            ServiceType = adapter.ServiceType,
                            Url = normalizedUrl
                        });

                        resolvedCount++;

                        _logger.LogInformation(
                            "Found song on {ServiceType}: SongId={SongId}, Url={Url}",
                            adapter.ServiceType, message.SongId, normalizedUrl);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not find song on {ServiceType}: SongId={SongId}",
                            adapter.ServiceType, message.SongId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error resolving song on {ServiceType}: SongId={SongId}",
                        adapter.ServiceType, message.SongId);
                    // Continue with other services
                }
            }

            // Update song status based on resolution results
            song.Status = resolvedCount == otherAdapters.Count
                ? SongStatus.Resolved
                : resolvedCount > 0
                    ? SongStatus.PartiallyResolved
                    : SongStatus.Failed;

            song.UpdatedAt = DateTime.UtcNow;
            await _songRepository.UpdateAsync(song, context.CancellationToken);

            // Update ShareRequest status to Completed
            var shareRequest = await _shareRequestRepository.GetBySongIdAsync(message.SongId, context.CancellationToken);
            if (shareRequest != null)
            {
                // Mark ShareRequest as Completed regardless of song resolution status
                // (Completed means processing finished, not that it was successful on all services)
                shareRequest.Status = ShareStatus.Completed;
                await _shareRequestRepository.UpdateAsync(shareRequest, context.CancellationToken);

                _logger.LogInformation(
                    "Updated ShareRequest status to Completed: ShareId={ShareId}, SongId={SongId}",
                    shareRequest.ShareId, message.SongId);
            }
            else
            {
                _logger.LogWarning(
                    "ShareRequest not found for SongId={SongId}, unable to update status",
                    message.SongId);
            }

            _logger.LogInformation(
                "Completed service resolution: SongId={SongId}, Status={Status}, ResolvedCount={ResolvedCount}/{TotalCount}",
                message.SongId, song.Status, resolvedCount, otherAdapters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SongMetadataResolved for SongId={SongId}", message.SongId);
            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
