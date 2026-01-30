using MassTransit;
using MusicShare.Api.Models;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
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
    UrlNormalizer urlNormalizer) : IShareRequestService
{
    public async Task<string> Create(
        string sourceUrl,
        ServiceType serviceType,
        CancellationToken cancellationToken)
    {
        var shareId = urlNormalizer.GenerateShareId();
        var correlationId = Guid.NewGuid();

        await shareRequestRepository.InsertAsync(new ShareRequest
        {
            ShareId = shareId,
            SourceUrl = sourceUrl,
            SourceService = serviceType,
            Status = ShareStatus.Pending,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await publishEndpoint.Publish(new SongShareSubmitted
        {
            ShareId = shareId,
            SourceUrl = sourceUrl,
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
