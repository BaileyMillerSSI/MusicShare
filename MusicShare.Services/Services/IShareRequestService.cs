using MusicShare.Contracts;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

public interface IShareRequestService
{
    /// <summary>Returns the canonical share ID when an existing or reconciled share is reused.</summary>
    Task<string> Create(
        string sourceUrl,
        ServiceType serviceType,
        CancellationToken cancellationToken);

    Task<ShareResultResponse?> GetByShareIdAsync(
        string shareId,
        CancellationToken cancellationToken);
}
