using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public interface IPublicMetricsSnapshotRepository
{
    Task<PublicMetricsSnapshot?> GetAsync(CancellationToken cancellationToken = default);
    Task<long> ReserveVersionAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Replaces the snapshot if it is not stale. Only a completed duplicate-share reconciliation
    /// may permit a lower total, and it still must have a newer reserved snapshot version.
    /// Ordinary candidates must also be newer than the persisted reconciliation decrease floor.
    /// </summary>
    Task<bool> TryReplaceAsync(
        PublicMetricsSnapshot snapshot,
        CancellationToken cancellationToken = default,
        bool allowReconciliationDecrease = false);
}
