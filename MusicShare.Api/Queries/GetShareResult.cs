using MediatR;
using MusicShare.Api.Models;
using MusicShare.Api.Services;

namespace MusicShare.Api.Queries;

public static class GetShareResult
{
    public record Query(string ShareId) : IRequest<Result>;

    public class Handler(
        IShareRequestService shareRequestService) : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var response = await shareRequestService.GetByShareIdAsync(request.ShareId, cancellationToken);

            return response == null
                ? Result.NotFound()
                : Result.Success(response);
        }
    }

    public record Result(bool Found, ShareResultResponse? Response)
    {
        public static Result NotFound() => new(false, null);

        public static Result Success(ShareResultResponse response) => new(true, response);
    }
}