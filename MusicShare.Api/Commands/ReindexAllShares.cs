using MediatR;
using MusicShare.Services.Services;

namespace MusicShare.Api.Commands;

public static class ReindexAllShares
{
    public record Request : IRequest<Response>;

    public class Handler(
        IShareRequestService shareRequestService) : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var count = await shareRequestService.ReindexAllCompletedAsync(cancellationToken);

            return Response.AsSuccess(count);
        }
    }

    public record Response(bool Success, int Count)
    {
        public static Response AsSuccess(int count) => new(true, count);
    }
}
