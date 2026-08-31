using MongoDB.Driver;
using MusicShare.Persistence.Entities;

namespace MusicShare.Persistence.Repositories;

public class PublicMetricsSnapshotRepository(IMusicShareDbContext context) : IPublicMetricsSnapshotRepository
{
    private readonly IMongoCollection<PublicMetricsSnapshot> _snapshots = context.PublicMetricsSnapshots;

    public async Task<PublicMetricsSnapshot?> GetAsync(CancellationToken cancellationToken = default) =>
        await _snapshots.Find(x => x.Id == PublicMetricsSnapshot.SingletonId).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryReplaceAsync(PublicMetricsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        snapshot.Id = PublicMetricsSnapshot.SingletonId;
        var filter = BuildNonRegressionFilter(snapshot.TotalCompletedSongs, snapshot.SnapshotVersion);

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
}
