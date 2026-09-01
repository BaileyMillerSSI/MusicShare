using MongoDB.Driver;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class PublicMetricsSnapshotRepository(IMusicShareDbContext context) : IPublicMetricsSnapshotRepository
{
    private const string RevisionCounterId = "public-metrics-revision";
    private readonly IMongoCollection<PublicMetricsSnapshot> _snapshots = context.PublicMetricsSnapshots;

    public async Task<PublicMetricsSnapshot?> GetAsync(CancellationToken cancellationToken = default) =>
        await _snapshots.Find(x => x.Id == PublicMetricsSnapshot.SingletonId).FirstOrDefaultAsync(cancellationToken);

    public async Task<long> ReserveVersionAsync(CancellationToken cancellationToken = default)
    {
        var counter = await _snapshots.FindOneAndUpdateAsync(
            Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.Id, RevisionCounterId),
            Builders<PublicMetricsSnapshot>.Update
                .Inc(x => x.SnapshotVersion, 1)
                .SetOnInsert(x => x.GeneratedAt, DateTime.UnixEpoch),
            new FindOneAndUpdateOptions<PublicMetricsSnapshot>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        return counter.SnapshotVersion;
    }

    public async Task<bool> TryReplaceAsync(
        PublicMetricsSnapshot snapshot,
        CancellationToken cancellationToken = default,
        bool allowReconciliationDecrease = false)
    {
        snapshot.Id = PublicMetricsSnapshot.SingletonId;
        var filter = allowReconciliationDecrease
            ? BuildNewerVersionFilter(snapshot.SnapshotVersion)
            : BuildNonRegressionFilter(snapshot.TotalCompletedSongs, snapshot.SnapshotVersion);
        var update = BuildSnapshotUpdate(snapshot, allowReconciliationDecrease);

        try
        {
            var result = await _snapshots.UpdateOneAsync(filter, update,
                new UpdateOptions { IsUpsert = true }, cancellationToken);
            return result.ModifiedCount > 0 || result.UpsertedId is not null || result.MatchedCount > 0;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var result = await _snapshots.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0 || result.MatchedCount > 0;
        }
    }

    internal static FilterDefinition<PublicMetricsSnapshot> BuildNonRegressionFilter(long candidateTotal, long candidateVersion) =>
        Builders<PublicMetricsSnapshot>.Filter.And(
            Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.Id, PublicMetricsSnapshot.SingletonId),
            BuildAboveReconciliationFloorFilter(candidateVersion),
            Builders<PublicMetricsSnapshot>.Filter.Or(
                Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.TotalCompletedSongs, candidateTotal),
                Builders<PublicMetricsSnapshot>.Filter.And(
                    Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.TotalCompletedSongs, candidateTotal),
                    Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.SnapshotVersion, candidateVersion))));

    internal static FilterDefinition<PublicMetricsSnapshot> BuildNewerVersionFilter(long candidateVersion) =>
        Builders<PublicMetricsSnapshot>.Filter.And(
            Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.Id, PublicMetricsSnapshot.SingletonId),
            BuildAboveReconciliationFloorFilter(candidateVersion),
            Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.SnapshotVersion, candidateVersion));

    private static FilterDefinition<PublicMetricsSnapshot> BuildAboveReconciliationFloorFilter(long candidateVersion) =>
        Builders<PublicMetricsSnapshot>.Filter.Or(
            Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.ReconciliationDecreaseVersionFloor, null),
            Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.ReconciliationDecreaseVersionFloor, candidateVersion));

    private static UpdateDefinition<PublicMetricsSnapshot> BuildSnapshotUpdate(
        PublicMetricsSnapshot snapshot,
        bool allowReconciliationDecrease)
    {
        var update = Builders<PublicMetricsSnapshot>.Update
            .Set(x => x.TotalCompletedSongs, snapshot.TotalCompletedSongs)
            .Set(x => x.SnapshotVersion, snapshot.SnapshotVersion)
            .Set(x => x.GeneratedAt, snapshot.GeneratedAt)
            .Set(x => x.ServiceCounts, snapshot.ServiceCounts)
            .Set(x => x.RecentSongs, snapshot.RecentSongs)
            .Set(x => x.DailyCompletedSongs, snapshot.DailyCompletedSongs);

        return allowReconciliationDecrease
            ? update.Set(x => x.ReconciliationDecreaseVersionFloor, snapshot.SnapshotVersion)
            : update;
    }
}
