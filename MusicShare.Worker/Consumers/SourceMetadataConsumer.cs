using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Consumes ResolveSourceMetadata commands from the saga.
/// Resolves metadata from the source service and publishes the result.
/// </summary>
public class SourceMetadataConsumer(
    IMusicServiceResolver serviceResolver,
    ISongRepository songRepository,
    ISongServiceLinkRepository linkRepository,
    IShareRequestRepository shareRequestRepository,
    ILogger<SourceMetadataConsumer> logger) : IConsumer<ResolveSourceMetadata>
{
    public async Task Consume(ConsumeContext<ResolveSourceMetadata> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Resolving source metadata: ShareId={ShareId}, Service={Service}, CorrelationId={CorrelationId}",
            message.ShareId, message.SourceService, message.CorrelationId);

        try
        {
            var adapter = serviceResolver.GetAdapter(message.SourceService);
            if (adapter == null)
            {
                logger.LogError("No adapter found for service type: {ServiceType}", message.SourceService);
                await PublishFailure(context, message, "No adapter found for service type");
                return;
            }

            var metadata = await adapter.ResolveMetadataAsync(message.SourceUrl, context.CancellationToken);
            if (metadata == null)
            {
                logger.LogWarning("Failed to resolve metadata from URL: {Url}", message.SourceUrl);
                await PublishFailure(context, message, "Could not resolve metadata from source URL");
                return;
            }

            logger.LogInformation("Resolved metadata: Title={Title}, Artists={Artists}",
                metadata.Title, string.Join(", ", metadata.Artists));

            // Create Song entity
            var song = new Song
            {
                Title = metadata.Title,
                Artists = [.. metadata.Artists],
                Album = metadata.Album,
                ArtworkUrl = metadata.ArtworkUrl,
                Duration = metadata.Duration,
                IsExplicit = metadata.IsExplicit,
                Status = SongStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            song = await songRepository.InsertAsync(song, context.CancellationToken);
            logger.LogInformation("Created Song entity: SongId={SongId}", song.Id);

            // Create source service link
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

            await linkRepository.InsertAsync(sourceLink, context.CancellationToken);

            // Update ShareRequest with SongId and status
            var shareRequest = await shareRequestRepository.GetByShareIdAsync(message.ShareId, context.CancellationToken);
            if (shareRequest != null)
            {
                shareRequest.SongId = song.Id;
                shareRequest.Status = ShareStatus.Processing;
                await shareRequestRepository.UpdateAsync(shareRequest, context.CancellationToken);
            }

            // Publish success event
            await context.Publish(new SourceMetadataResolved
            {
                CorrelationId = message.CorrelationId,
                SongId = song.Id,
                ShareId = message.ShareId,
                SourceService = message.SourceService,
                Metadata = new SongMetadataPayload
                {
                    Title = metadata.Title,
                    Artists = [.. metadata.Artists],
                    Album = metadata.Album,
                    ArtworkUrl = metadata.ArtworkUrl,
                    Duration = metadata.Duration,
                    IsExplicit = metadata.IsExplicit
                }
            });

            logger.LogInformation(
                "Successfully resolved source metadata: ShareId={ShareId}, SongId={SongId}",
                message.ShareId, song.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving source metadata for ShareId={ShareId}", message.ShareId);
            await PublishFailure(context, message, ex.Message);
            throw; // Re-throw to trigger MassTransit retry
        }
    }

    private static async Task PublishFailure(
        ConsumeContext<ResolveSourceMetadata> context,
        ResolveSourceMetadata message,
        string errorMessage)
    {
        await context.Publish(new SourceMetadataFailed
        {
            CorrelationId = message.CorrelationId,
            ShareId = message.ShareId,
            ErrorMessage = errorMessage
        });
    }
}
