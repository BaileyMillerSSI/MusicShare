using MediatR;
using MusicShare.Services.Services;

namespace MusicShare.Api.Queries;

public static class GetAllShareIds
{
    public record Query : IRequest<Result>;

    public class Handler(
        IShareRequestService shareRequestService) : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var shareIds = await shareRequestService.GetAllCompletedShareIdsAsync(cancellationToken);

            return Result.Success(shareIds);
        }
    }

    public record Result(IReadOnlyList<string> ShareIds)
    {
        public static Result Success(IReadOnlyList<string> shareIds) => new(shareIds);
    }
}
