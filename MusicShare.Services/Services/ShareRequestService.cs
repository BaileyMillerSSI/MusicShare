using MassTransit;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

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

        // Check if this song already exists via SongServiceLink
        // This catches duplicates regardless of which service the song was originally submitted from
        if (!string.IsNullOrEmpty(serviceTrackId))
        {
            var existingLink = await linkRepository.GetByServiceAndSongIdAsync(
                serviceType, serviceTrackId, cancellationToken);

            if (existingLink != null)
            {
                var existingRequest = await shareRequestRepository.GetBySongIdAsync(
                    existingLink.SongId, cancellationToken);

                if (existingRequest != null)
                {
                    return existingRequest.ShareId;
                }
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

    public async Task<List<string>> GetAllCompletedShareIdsAsync(
        CancellationToken cancellationToken) =>
        await shareRequestRepository.GetAllCompletedShareIdsAsync(cancellationToken);

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
                        IsExplicit = song.IsExplicit,
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
