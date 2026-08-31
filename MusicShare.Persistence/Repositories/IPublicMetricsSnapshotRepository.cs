using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public interface IPublicMetricsSnapshotRepository
{
    Task<PublicMetricsSnapshot?> GetAsync(CancellationToken cancellationToken = default);
    Task<long> ReserveVersionAsync(CancellationToken cancellationToken = default);
    Task<bool> TryReplaceAsync(PublicMetricsSnapshot snapshot, CancellationToken cancellationToken = default);
}
