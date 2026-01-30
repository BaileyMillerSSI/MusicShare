using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Worker.Services;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumes SongShareSubmitted messages and resolves song metadata from the source service.
/// This consumer is idempotent and retry-safe.
/// </summary>
public class MetadataResolverConsumer : IConsumer<SongShareSubmitted>
{
    private readonly ILogger<MetadataResolverConsumer> _logger;
    private readonly MusicServiceResolver _serviceResolver;
    private readonly ISongRepository _songRepository;
    private readonly ISongServiceLinkRepository _linkRepository;
    private readonly IShareRequestRepository _shareRequestRepository;

    public MetadataResolverConsumer(
        ILogger<MetadataResolverConsumer> logger,
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

    public async Task Consume(ConsumeContext<SongShareSubmitted> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing SongShareSubmitted: ShareId={ShareId}, SourceService={SourceService}",
            message.ShareId, message.SourceService);

        try
        {
            // Get the appropriate adapter for the source service
            var adapter = _serviceResolver.GetAdapter(message.SourceService);
            if (adapter == null)
            {
                _logger.LogError("No adapter found for service type: {ServiceType}", message.SourceService);
                await UpdateShareRequestStatus(message.ShareId, ShareStatus.Failed);
                return;
            }

            // Resolve metadata from the source URL
            var metadata = await adapter.ResolveMetadataAsync(message.SourceUrl, context.CancellationToken);
            if (metadata == null)
            {
                _logger.LogWarning("Failed to resolve metadata from URL: {Url}", message.SourceUrl);
                await UpdateShareRequestStatus(message.ShareId, ShareStatus.Failed);
                return;
            }

            _logger.LogInformation("Resolved metadata: Title={Title}, Artists={Artists}",
                metadata.Title, string.Join(", ", metadata.Artists));

            // Create or update the Song entity
            var song = new Song
            {
                Title = metadata.Title,
                Artists = [.. metadata.Artists],
                Album = metadata.Album,
                ArtworkUrl = metadata.ArtworkUrl,
                Duration = metadata.Duration,
                Status = SongStatus.Pending, // Will be updated as links are resolved
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            song = await _songRepository.InsertAsync(song, context.CancellationToken);

            _logger.LogInformation("Created Song entity: SongId={SongId}", song.Id);

            // Create the source service link
            var normalizedUrl = adapter.NormalizeUrl(message.SourceUrl);
            var serviceSongId = adapter.ExtractSongId(message.SourceUrl) ?? message.SourceUrl;

            var sourceLink = new SongServiceLink
            {
                SongId = song.Id,
                ServiceType = message.SourceService,
                ServiceSongId = serviceSongId,
                OriginalUrl = message.SourceUrl,
                NormalizedUrl = normalizedUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _linkRepository.InsertAsync(sourceLink, context.CancellationToken);

            // Update the ShareRequest with the SongId
            var shareRequest = await _shareRequestRepository.GetByShareIdAsync(message.ShareId, context.CancellationToken);
            if (shareRequest != null)
            {
                shareRequest.SongId = song.Id;
                shareRequest.Status = ShareStatus.Processing;
                await _shareRequestRepository.UpdateAsync(shareRequest, context.CancellationToken);
            }

            // Publish SongMetadataResolved event
            await context.Publish(new SongMetadataResolved
            {
                SongId = song.Id,
                Metadata = metadata
            });

            _logger.LogInformation(
                "Successfully processed metadata resolution for ShareId={ShareId}, SongId={SongId}",
                message.ShareId, song.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SongShareSubmitted for ShareId={ShareId}", message.ShareId);
            await UpdateShareRequestStatus(message.ShareId, ShareStatus.Failed);
            throw; // Re-throw to trigger MassTransit retry
        }
    }

    private async Task UpdateShareRequestStatus(string shareId, ShareStatus status)
    {
        try
        {
            var shareRequest = await _shareRequestRepository.GetByShareIdAsync(shareId);
            if (shareRequest != null)
            {
                shareRequest.Status = status;
                await _shareRequestRepository.UpdateAsync(shareRequest);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ShareRequest status for ShareId={ShareId}", shareId);
        }
    }
}
