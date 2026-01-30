using MassTransit;
using MusicShare.Api.Models;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.MusicAdapters.Services;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Api.Services;

public interface IShareRequestService
{
    Task<string> Create(
        string sourceUrl,
        ServiceType serviceType,
        CancellationToken cancellationToken);

    Task<ShareResultResponse?> GetByShareIdAsync(
        string shareId,
        CancellationToken cancellationToken);
}

public class ShareRequestService(
    IPublishEndpoint publishEndpoint,
    IShareRequestRepository shareRequestRepository,
    ISongRepository songRepository,
    ISongServiceLinkRepository linkRepository,
    IMusicServiceResolver musicServiceResolver) : IShareRequestService
{
   
    public async Task<string> Create(
        string sourceUrl,
        ServiceType serviceType,
        CancellationToken cancellationToken)
    {
        var adapter = musicServiceResolver.GetAdapter(serviceType);

        // Extract canonical track ID for duplicate detection
        var serviceTrackId = adapter!.ExtractSongId(sourceUrl);

        // Check for existing ShareRequest with same track
        if (!string.IsNullOrEmpty(serviceTrackId))
        {
            var existing = await shareRequestRepository.GetByServiceTrackIdAsync(
                serviceType, serviceTrackId, cancellationToken);

            if (existing != null)
            {
                return existing.ShareId;
            }
        }

        var correlationId = Guid.NewGuid();
        var shareId = correlationId.ToString("N")[..12];

        await shareRequestRepository.InsertAsync(new ShareRequest
        {
            ShareId = shareId,
            SourceUrl = adapter!.NormalizeUrl(sourceUrl),
            SourceService = serviceType,
            ServiceTrackId = serviceTrackId,
            Status = ShareStatus.Pending,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await publishEndpoint.Publish(new SongShareSubmitted
        {
            ShareId = shareId,
            SourceUrl = adapter!.NormalizeUrl(sourceUrl),
            SourceService = serviceType,
            CorrelationId = correlationId
        }, cancellationToken);

        return shareId;
    }

    public async Task<ShareResultResponse?> GetByShareIdAsync(
        string shareId,
        CancellationToken cancellationToken)
    {
        var shareRequest = await shareRequestRepository.GetByShareIdAsync(shareId, cancellationToken);
        if (shareRequest == null)
        {
            return null;
        }

        var response = new ShareResultResponse
        {
            ShareId = shareRequest.ShareId,
            Status = shareRequest.Status.ToString(),
            Song = null
        };

        if (!string.IsNullOrEmpty(shareRequest.SongId))
        {
            var song = await songRepository.GetByIdAsync(shareRequest.SongId, cancellationToken);
            if (song != null)
            {
                var links = await linkRepository.GetBySongIdAsync(song.Id, cancellationToken);

                response = response with
                {
                    Song = new SongDetails
                    {
                        Id = song.Id,
                        Title = song.Title,
                        Artists = song.Artists,
                        Album = song.Album,
                        ArtworkUrl = song.ArtworkUrl,
                        Duration = song.Duration,
                        Status = song.Status.ToString(),
                        Links = [.. links.Select(l => new ServiceLink
                        {
                            ServiceType = l.ServiceType,
                            Url = l.NormalizedUrl
                        })]
                    }
                };
            }
        }

        return response;
    }
}
