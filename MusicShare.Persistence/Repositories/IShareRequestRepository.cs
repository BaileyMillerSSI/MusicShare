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
    Task<ShareRequest?> GetBySourceIdentityKeyAsync(string sourceIdentityKey, CancellationToken cancellationToken = default);
    Task<ShareReservation> ReserveBySourceIdentityAsync(ShareRequest request, CancellationToken cancellationToken = default);
    Task<ShareRequest?> ResolveCanonicalAsync(ShareRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareRequest>> GetByShareIdsAsync(IReadOnlyCollection<string> shareIds, CancellationToken cancellationToken = default);
    /// <summary>Returns only aliases whose direct canonical target is one of the supplied pair.</summary>
    Task<IReadOnlyList<ShareRequest>> GetAliasesTargetingShareIdsAsync(IReadOnlyCollection<string> shareIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareRequest>> GetBySongIdsAsync(IReadOnlyCollection<string> songIds, CancellationToken cancellationToken = default);
    Task<ReconciliationWriteResult> TryReconcileAsync(ReconciliationWrite write, CancellationToken cancellationToken = default);
    Task<ShareRequest> InsertAsync(ShareRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShareRequest request, CancellationToken cancellationToken = default);
    Task<long> GetCompletedDistinctSongCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompletedShareRequest>> GetRecentCompletedDistinctAsync(int maximum, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyCompletedSongCount>> GetCompletedDistinctSongCountsByDayAsync(DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default);
}

public record CompletedShareRequest(string SongId, string ShareId, ServiceType SourceService, DateTime CreatedAt);
public record DailyCompletedSongCount(DateTime DayStart, long Count);
public record ShareReservation(ShareRequest Request, bool Inserted);
public record ReconciliationWrite(
    string CanonicalShareId,
    string AliasShareId,
    string ReconciliationId,
    string Fingerprint,
    string CanonicalSongId,
    string AliasSongId,
    ShareStatus CanonicalStatus,
    ShareStatus AliasStatus,
    DateTime CanonicalCreatedAt,
    DateTime AliasCreatedAt,
    ServiceType CanonicalSourceService,
    string? CanonicalServiceTrackId,
    string? CanonicalSourceIdentityKey,
    long CanonicalPreClaimVersion = 0,
    long AliasPreClaimVersion = 0);
public record ReconciliationWriteResult(bool Succeeded, bool Changed, string? Error = null);
