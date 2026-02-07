using MusicShare.Contracts;
using MusicShare.Services.Models;

namespace MusicShare.Services.Services;

public interface IShareRequestService
{
    Task<string> Create(
        string sourceUrl,
        ServiceType serviceType,
        CancellationToken cancellationToken);

    Task<ShareResultResponse?> GetByShareIdAsync(
        string shareId,
        CancellationToken cancellationToken);

    Task<List<string>> GetAllCompletedShareIdsAsync(
        CancellationToken cancellationToken);
}
