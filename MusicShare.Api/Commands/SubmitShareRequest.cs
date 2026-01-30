using MediatR;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.MusicAdapters;
using MusicShare.MusicAdapters.Services;
using System.ComponentModel.DataAnnotations;

namespace MusicShare.Api.Commands;

public record SubmitShareRequest(
    [Required, Url] string Url
) : IRequest<SubmitShareCommandResponse>;

public record SubmitShareCommandResponse(bool Success, string? ShareId, string? Status, string? Error)
{
    public static SubmitShareCommandResponse AsFailure(string error) =>
        new(false, null, null, error);

    public static SubmitShareCommandResponse AsSuccess(string shareId, ShareStatus status) =>
        new(true, shareId, status.ToString(), null);
}

public class SubmitShareCommandHandler(
    IShareRequestService shareRequestService,
    IMusicServiceResolver musicResolver) : IRequestHandler<SubmitShareRequest, SubmitShareCommandResponse>
{
    public async Task<SubmitShareCommandResponse> Handle(SubmitShareRequest request, CancellationToken cancellationToken)
    {
        var serviceType = musicResolver.DetectServiceType(request.Url);
        if (serviceType == null || serviceType == ServiceType.Unknown)
        {
            return SubmitShareCommandResponse.AsFailure("Unsupported music service URL");
        }

        var shareId = await shareRequestService.Create(
            request.Url,
            serviceType.Value,
            cancellationToken);

        return SubmitShareCommandResponse.AsSuccess(shareId, ShareStatus.Pending);
    }
}
