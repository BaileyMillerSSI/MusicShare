using System.ComponentModel.DataAnnotations;
using MassTransit;
using MediatR;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Api.Commands;

public record SubmitShareRequest(
    [Required, Url] string Url
) : IRequest<SubmitShareCommandResponse>;

public record SubmitShareCommandResponse(bool Success, string? ShareId, string? Status, string? Error)
{
    public static SubmitShareCommandResponse ToFailure(string error) =>
        new(false, null, null, error);

    public static SubmitShareCommandResponse ToSuccess(string shareId, ShareStatus status) =>
        new(true, shareId, status.ToString(), null);
}

public class SubmitShareCommandHandler(
    IPublishEndpoint publishEndpoint,
    IShareRequestRepository shareRequestRepository,
    UrlNormalizer urlNormalizer) : IRequestHandler<SubmitShareRequest, SubmitShareCommandResponse>
{
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IShareRequestRepository _shareRequestRepository = shareRequestRepository;
    private readonly UrlNormalizer _urlNormalizer = urlNormalizer;

    public async Task<SubmitShareCommandResponse> Handle(SubmitShareRequest request, CancellationToken cancellationToken)
    {
        var serviceType = _urlNormalizer.DetectServiceType(request.Url);
        if (serviceType == ServiceType.Unknown)
        {
            return SubmitShareCommandResponse.ToFailure("Unsupported music service URL");
        }

        var shareId = _urlNormalizer.GenerateShareId();
        var correlationId = Guid.NewGuid();

        await _shareRequestRepository.InsertAsync(new ShareRequest
        {
            ShareId = shareId,
            SourceUrl = request.Url,
            SourceService = serviceType,
            Status = ShareStatus.Pending,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _publishEndpoint.Publish(new SongShareSubmitted
        {
            ShareId = shareId,
            SourceUrl = request.Url,
            SourceService = serviceType,
            CorrelationId = correlationId
        }, cancellationToken);

        return SubmitShareCommandResponse.ToSuccess(shareId, ShareStatus.Pending);
    }
}
