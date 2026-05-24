using MediatR;
using MusicShare.Services.Services;

namespace MusicShare.Api.Commands;

public static class ReindexSong
{
    public record Request(string SongId) : IRequest<Response>;

    public class Handler(
        IShareRequestService shareRequestService) : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var found = await shareRequestService.ReindexSongAsync(request.SongId, cancellationToken);

            return found ? Response.AsSuccess() : Response.NotFound();
        }
    }

    public record Response(bool Success, bool Found)
    {
        public static Response AsSuccess() => new(true, true);
        public static Response NotFound() => new(false, false);
    }
}
