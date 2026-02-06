using MediatR;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services;
using System.ComponentModel.DataAnnotations;

namespace MusicShare.Api.Commands;

public static class SubmitShare
{
    public record Request(
    [Required, Url] string Url
) : IRequest<Response>;

    public class Handler(
    IShareRequestService shareRequestService,
    IMusicServiceResolver musicResolver) : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var serviceType = musicResolver.DetectServiceType(request.Url);
            if (serviceType == null || serviceType == ServiceType.Unknown)
            {
                return Response.AsFailure("Unsupported music service URL");
            }

            var shareId = await shareRequestService.Create(
                request.Url,
                serviceType.Value,
                cancellationToken);

            return Response.AsSuccess(shareId, ShareStatus.Pending);
        }
    }

    public record Response(bool Success, string? ShareId, string? Status, string? Error)
    {
        public static Response AsFailure(string error) =>
            new(false, null, null, error);

        public static Response AsSuccess(string shareId, ShareStatus status) =>
            new(true, shareId, status.ToString(), null);
    }
}