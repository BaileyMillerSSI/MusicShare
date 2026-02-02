using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Worker.Consumers;

/// <summary>
/// Base class for service-specific link resolution consumers.
/// Handles the common logic for finding a song on a music service and creating the link.
/// </summary>
public abstract class ServiceLinkConsumerBase(
    ISongServiceLinkRepository linkRepository,
    ILogger logger) : IConsumer<ResolveServiceLink>
{
    /// <summary>
    /// The service type this consumer handles.
    /// </summary>
    protected abstract ServiceType ServiceType { get; }

    /// <summary>
    /// Gets the adapter for this service.
    /// </summary>
    protected abstract IMusicServiceAdapter GetAdapter();

    public async Task Consume(ConsumeContext<ResolveServiceLink> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Resolving {Service} link: SongId={SongId}, CorrelationId={CorrelationId}",
            ServiceType, message.SongId, message.CorrelationId);

        try
        {
            var adapter = GetAdapter();

            // Check if we already have a link for this service (idempotent)
            var existingLink = await linkRepository.GetBySongIdAndServiceAsync(
                message.SongId, ServiceType, context.CancellationToken);

            if (existingLink != null)
            {
                logger.LogInformation(
                    "Link already exists for SongId={SongId}, Service={Service}",
                    message.SongId, ServiceType);

                // Still publish success so saga can track completion
                await PublishSuccess(context, message, existingLink.NormalizedUrl);
                return;
            }

            // Convert payload to SongMetadata for adapter
            var metadata = new SongMetadata
            {
                Title = message.Metadata.Title,
                Artists = message.Metadata.Artists,
                Album = message.Metadata.Album,
                ArtworkUrl = message.Metadata.ArtworkUrl,
                Duration = message.Metadata.Duration,
                IsExplicit = message.Metadata.IsExplicit
            };

            // Try to find the song on this service
            var foundUrl = await adapter.FindSongAsync(metadata, context.CancellationToken);

            if (string.IsNullOrEmpty(foundUrl))
            {
                logger.LogWarning(
                    "Song not found on {Service}: SongId={SongId}",
                    ServiceType, message.SongId);

                await PublishFailure(context, message, "Song not found on service");
                return;
            }

            // Create the service link
            var normalizedUrl = adapter.NormalizeUrl(foundUrl);
            var serviceSongId = adapter.ExtractSongId(foundUrl) ?? foundUrl;

            var link = new SongServiceLink
            {
                SongId = message.SongId,
                ServiceType = ServiceType,
                ServiceSongId = serviceSongId,
                OriginalUrl = foundUrl,
                NormalizedUrl = normalizedUrl,
                CreatedAt = DateTime.UtcNow
            };

            await linkRepository.InsertAsync(link, context.CancellationToken);

            logger.LogInformation(
                "Created {Service} link: SongId={SongId}, Url={Url}",
                ServiceType, message.SongId, normalizedUrl);

            await PublishSuccess(context, message, normalizedUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error resolving {Service} link for SongId={SongId}",
                ServiceType, message.SongId);

            await PublishFailure(context, message, ex.Message);
            // Don't re-throw - we want the saga to continue with other services
        }
    }

    private async Task PublishSuccess(
        ConsumeContext<ResolveServiceLink> context,
        ResolveServiceLink message,
        string resolvedUrl)
    {
        await context.Publish(new ServiceLinkResolved
        {
            CorrelationId = message.CorrelationId,
            SongId = message.SongId,
            ServiceType = ServiceType,
            ResolvedUrl = resolvedUrl
        });
    }

    private async Task PublishFailure(
        ConsumeContext<ResolveServiceLink> context,
        ResolveServiceLink message,
        string errorMessage)
    {
        await context.Publish(new ServiceLinkFailed
        {
            CorrelationId = message.CorrelationId,
            SongId = message.SongId,
            ServiceType = ServiceType,
            ErrorMessage = errorMessage
        });
    }
}
