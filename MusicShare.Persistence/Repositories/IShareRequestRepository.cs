using MusicShare.Contracts;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public interface IShareRequestRepository
{
    Task<ShareRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ShareRequest?> GetByShareIdAsync(string shareId, CancellationToken cancellationToken = default);
    Task<ShareRequest?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<ShareRequest?> GetBySongIdAsync(string songId, CancellationToken cancellationToken = default);
    Task<ShareRequest?> GetByServiceTrackIdAsync(ServiceType serviceType, string serviceTrackId, CancellationToken cancellationToken = default);
    Task<ShareRequest> InsertAsync(ShareRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShareRequest request, CancellationToken cancellationToken = default);
}
