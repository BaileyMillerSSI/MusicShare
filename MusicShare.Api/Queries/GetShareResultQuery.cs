using MediatR;
using MusicShare.Api.Models;
using MusicShare.Api.Services;

namespace MusicShare.Api.Queries;

public record GetShareResultQuery(
    string ShareId
) : IRequest<GetShareResultQueryResult>;

public record GetShareResultQueryResult(bool Found, ShareResultResponse? Response)
{
    public static GetShareResultQueryResult NotFound() => new(false, null);

    public static GetShareResultQueryResult Success(ShareResultResponse response) => new(true, response);
}

public class GetShareResultQueryHandler(
    IShareRequestService shareRequestService) : IRequestHandler<GetShareResultQuery, GetShareResultQueryResult>
{
    public async Task<GetShareResultQueryResult> Handle(GetShareResultQuery request, CancellationToken cancellationToken)
    {
        var response = await shareRequestService.GetByShareIdAsync(request.ShareId, cancellationToken);

        return response == null
            ? GetShareResultQueryResult.NotFound()
            : GetShareResultQueryResult.Success(response);
    }
}
