using System.ComponentModel.DataAnnotations;
using MediatR;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

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
    UrlNormalizer urlNormalizer) : IRequestHandler<SubmitShareRequest, SubmitShareCommandResponse>
{
    public async Task<SubmitShareCommandResponse> Handle(SubmitShareRequest request, CancellationToken cancellationToken)
    {
        var serviceType = urlNormalizer.DetectServiceType(request.Url);
        if (serviceType == ServiceType.Unknown)
        {
            return SubmitShareCommandResponse.AsFailure("Unsupported music service URL");
        }

        var shareId = await shareRequestService.Create(
            request.Url,
            serviceType,
            cancellationToken);

        return SubmitShareCommandResponse.AsSuccess(shareId, ShareStatus.Pending);
    }
}
