using MediatR;
using MusicShare.Api.Models;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Api.Queries;

public record GetShareResultQuery(
    string ShareId
) : IRequest<GetShareResultQueryResult>;

public record GetShareResultQueryResult(bool Found, ShareResultResponse? Response)
{
    public static GetShareResultQueryResult NotFound() => new(false, null);

    public static GetShareResultQueryResult Success(ShareResultResponse response) => new(true, response);
}

public class GetShareResultQueryHandler : IRequestHandler<GetShareResultQuery, GetShareResultQueryResult>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISongRepository _songRepository;
    private readonly ISongServiceLinkRepository _linkRepository;

    public GetShareResultQueryHandler(
        IShareRequestRepository shareRequestRepository,
        ISongRepository songRepository,
        ISongServiceLinkRepository linkRepository)
    {
        _shareRequestRepository = shareRequestRepository;
        _songRepository = songRepository;
        _linkRepository = linkRepository;
    }

    public async Task<GetShareResultQueryResult> Handle(GetShareResultQuery request, CancellationToken cancellationToken)
    {
        var shareRequest = await _shareRequestRepository
            .GetByShareIdAsync(
            request.ShareId,
            cancellationToken);

        if (shareRequest == null)
        {
            return GetShareResultQueryResult.NotFound();
        }

        var response = new ShareResultResponse
        {
            ShareId = shareRequest.ShareId,
            Status = shareRequest.Status.ToString(),
            Song = null
        };

        if (!string.IsNullOrEmpty(shareRequest.SongId))
        {
            var song = await _songRepository.GetByIdAsync(shareRequest.SongId, cancellationToken);
            if (song != null)
            {
                var links = await _linkRepository.GetBySongIdAsync(song.Id, cancellationToken);

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
                        Links = links.Select(l => new ServiceLink
                        {
                            ServiceType = l.ServiceType,
                            Url = l.NormalizedUrl
                        }).ToList()
                    }
                };
            }
        }

        return GetShareResultQueryResult.Success(response);
    }
}
