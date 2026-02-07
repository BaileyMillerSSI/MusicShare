using Microsoft.Extensions.Logging;
using MusicShare.Contracts;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Services.Services;

public class ShareStatusService(IShareRequestRepository shareRequestRepository, ILogger<ShareStatusService> logger) : IShareStatusService
{
    private readonly IShareRequestRepository _shareRequestRepository = shareRequestRepository;
    private readonly ILogger<ShareStatusService> _logger = logger;

    public async Task UpdateStatusAsync(string shareId, ShareStatus status, CancellationToken cancellationToken = default)
    {
        var shareRequest = await _shareRequestRepository.GetByShareIdAsync(shareId, cancellationToken);
        if (shareRequest is null)
        {
            _logger.LogWarning("ShareRequest not found for ShareId={ShareId}, skipping status update", shareId);
            return;
        }

        shareRequest.Status = status;
        await _shareRequestRepository.UpdateAsync(shareRequest, cancellationToken);

        _logger.LogInformation("Updated share request {ShareId} status to {Status}", shareId, status);
    }
}
