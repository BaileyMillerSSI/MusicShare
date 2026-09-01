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

        try
        {
            var result = await _snapshots.ReplaceOneAsync(filter, snapshot,
                new ReplaceOptions { IsUpsert = true }, cancellationToken);
            return result.ModifiedCount > 0 || result.UpsertedId is not null || result.MatchedCount > 0;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var result = await _snapshots.ReplaceOneAsync(filter, snapshot, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0 || result.MatchedCount > 0;
        }
    }

    internal static FilterDefinition<PublicMetricsSnapshot> BuildNonRegressionFilter(long candidateTotal, long candidateVersion) =>
        Builders<PublicMetricsSnapshot>.Filter.And(
            Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.Id, PublicMetricsSnapshot.SingletonId),
            Builders<PublicMetricsSnapshot>.Filter.Or(
                Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.TotalCompletedSongs, candidateTotal),
                Builders<PublicMetricsSnapshot>.Filter.And(
                    Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.TotalCompletedSongs, candidateTotal),
                    Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.SnapshotVersion, candidateVersion))));

    internal static FilterDefinition<PublicMetricsSnapshot> BuildNewerVersionFilter(long candidateVersion) =>
        Builders<PublicMetricsSnapshot>.Filter.And(
            Builders<PublicMetricsSnapshot>.Filter.Eq(x => x.Id, PublicMetricsSnapshot.SingletonId),
            Builders<PublicMetricsSnapshot>.Filter.Lt(x => x.SnapshotVersion, candidateVersion));
}
